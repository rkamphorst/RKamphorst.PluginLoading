using Amazon.ECS;
using Amazon.ECS.Model;
using Microsoft.Extensions.Logging;
using RKamphorst.PluginLoading.Aws.Options;
using KeyValuePair = Amazon.ECS.Model.KeyValuePair;
using Task = System.Threading.Tasks.Task;

namespace RKamphorst.PluginLoading.Aws;

public class EcsServiceUpdater : IEcsServiceUpdater
{
    private readonly IAmazonECS _ecsClient;
    private readonly EcsServiceUpdaterOptions _options;
    private readonly IPluginLibraryTimestampProvider _timestampProvider;
    private readonly ILogger<EcsServiceUpdater> _logger;

    public EcsServiceUpdater(
        IAmazonECS ecsClient,
        EcsServiceUpdaterOptions? options,
        IPluginLibraryTimestampProvider timestampProvider,
        ILogger<EcsServiceUpdater> logger
    )
    {
        _ecsClient = ecsClient;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timestampProvider = timestampProvider;
        _logger = logger;
    }

    internal Func<DateTimeOffset> GetNowTime { get; set; } = () => DateTimeOffset.UtcNow; 
    
    public async Task UpdateAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset? versionAtDate = await _timestampProvider.GetTimestampAsync(cancellationToken);

        if (!versionAtDate.HasValue)
        {
            _logger.LogInformation(
                "Library source gave null timestamp, VersionAtDate will be set ad hoc"
            );
        }
        else
        {
            _logger.LogInformation(
                "Library source gave VersionAtDate {VersionAtDate}", versionAtDate.Value
            );
        }

        ServiceAndCluster[]? clusterServicePairs = _options.Services;

        if (clusterServicePairs == null || clusterServicePairs.Length == 0)
        {
            _logger.LogWarning($"Updater options: {nameof(EcsServiceUpdaterOptions.Services)} is empty");
            return;
        }

        var ctr = 0;
        var updateTasks = new List<Task>();
        foreach (ServiceAndCluster pair in clusterServicePairs)
        {
            var clusterName = pair.Cluster;
            var serviceName = pair.Service;

            if (string.IsNullOrWhiteSpace(clusterName) || string.IsNullOrWhiteSpace(serviceName))
            {
                _logger.LogWarning(
                    $"Updater options: {nameof(_options.Services)} " +
                    $"has empty {nameof(pair.Service)} or {nameof(pair.Cluster)} " +
                    $"at index {{EmptyIndex}}",
                    ctr
                );
            }
            else
            {
                updateTasks.Add(UpdateAsync(clusterName, serviceName, versionAtDate, cancellationToken));
            }

            ctr++;
        }

        await Task.WhenAll(updateTasks);
    }
    
    public async Task UpdateAsync(string clusterName, string serviceName, DateTimeOffset? versionAtDate, CancellationToken cancellationToken)
    {
        TaskDefinition? taskDefinition = await GetCurrentTaskDefinitionAsync(clusterName, serviceName, cancellationToken);
        if (taskDefinition == null)
        {
            _logger.LogWarning(
                "Could not get task definition for service {Cluster}/{Service}", 
                clusterName,
                serviceName);
            return;
        }
        
        var newTaskDefinitionRevision = await TryUpdateTaskDefinitionAsync(taskDefinition, versionAtDate, cancellationToken);

        if (newTaskDefinitionRevision == null)
        {
            _logger.LogInformation(
                "Service {Cluster}/{Service} needs no update, " +
                "task definition remains at revision {TaskDefinitionRevision}",
                clusterName, serviceName, $"{taskDefinition.Family}:{taskDefinition.Revision}"
            );
            return;
        }
        
        _logger.LogInformation(
            "Updating service {Cluster}/{Service} " +
            "to use task definition revision {NewTaskDefinitionRevision}",
            clusterName, serviceName, newTaskDefinitionRevision
        );

        await _ecsClient.UpdateServiceAsync(new UpdateServiceRequest
        {
            Cluster = clusterName,
            Service = serviceName,
            TaskDefinition = newTaskDefinitionRevision
        }, cancellationToken);
    }

    private readonly Dictionary<string, Task<string?>> _updatedTaskDefinitions = new();
    
    private Task<string?> TryUpdateTaskDefinitionAsync(TaskDefinition taskDefinition, DateTimeOffset? versionAtDate,
        CancellationToken cancellationToken)
    {
        var taskDefinitionRevision = $"{taskDefinition.Family}:{taskDefinition.Revision}";
        return !_updatedTaskDefinitions.TryGetValue(taskDefinition.Family, out Task<string?>? tsk)
            ? (_updatedTaskDefinitions[taskDefinition.Family] = DoUpdateAsync())
            : tsk;
        
        async Task<string?> DoUpdateAsync()
        {
            if (!TryUpdateContainerDefinitions(taskDefinition.ContainerDefinitions, versionAtDate))
            {
                _logger.LogDebug(
            "Not Updating task definition {TaskDefinitionRevision}, " +
                    "it already uses VersionAtDate {VersionAtDate}",
                    taskDefinitionRevision, versionAtDate);
                return null;
            }

            _logger.LogDebug(
                "Updating task definition {TaskDefinitionRevision} to use VersionAtDate {VersionAtDate}",
                taskDefinitionRevision, versionAtDate);

            RegisterTaskDefinitionResponse? registerResponse =
                await _ecsClient.RegisterTaskDefinitionAsync(CreateRegisterTaskDefinitionRequest(taskDefinition),
                    cancellationToken);

            TaskDefinition? newTaskDefinition = registerResponse.TaskDefinition;
            var newTaskDefinitionRevision = $"{newTaskDefinition.Family}:{newTaskDefinition.Revision}";
            _logger.LogInformation(
                "New task definition revision {NewTaskDefinitionRevision} uses VersionAtDate {VersionAtDate}",
                newTaskDefinitionRevision, versionAtDate
                );
            return newTaskDefinitionRevision;
        }
    }

    private bool TryUpdateContainerDefinitions(
        IEnumerable<ContainerDefinition> containerDefinitions, DateTimeOffset? versionAtDate)
    {
        var isUpdated = false;
        foreach (ContainerDefinition containerDefinition in containerDefinitions)
        {
            List<KeyValuePair> versionAtDateVars =
                containerDefinition.Environment
                    .Where(kvp => kvp.Name == Constants.DefaultVersionAtDateEnvironmentVariable)
                    .ToList();

            if (versionAtDateVars.Count != 1 ||
                !DateTimeOffset.TryParse(versionAtDateVars[0].Value, out DateTimeOffset dt) ||
                (versionAtDate.HasValue && dt != versionAtDate)
               )
            {
                var value = versionAtDate ?? GetNowTime();
                if (!versionAtDate.HasValue)
                {
                    _logger.LogInformation(
                        "Using ad-hoc VersionAtDate {VersionAtDate} to update task definition",
                        value
                        );
                }
                
                List<KeyValuePair> newEnvironment =
                    containerDefinition.Environment
                        .Where(kvp => kvp.Name != Constants.DefaultVersionAtDateEnvironmentVariable)
                        .Concat(new KeyValuePair[]
                        {
                            new() { Name = Constants.DefaultVersionAtDateEnvironmentVariable, Value = value.ToString("O") }
                        }).ToList();
                containerDefinition.Environment = newEnvironment;
                isUpdated = true;
            }
        }
        return isUpdated;
    }

    private async Task<TaskDefinition?> GetCurrentTaskDefinitionAsync(string clusterName, string serviceName, CancellationToken cancellationToken)
    {
        Service? service;
        try
        {
            DescribeServicesResponse? describeServicesResponse = await _ecsClient.DescribeServicesAsync(new DescribeServicesRequest
            {
                Cluster = clusterName,
                Services = new List<string> { serviceName }
            }, cancellationToken);
            
            service = describeServicesResponse.Services.SingleOrDefault();
            if (service == null)
            {
                _logger.LogWarning("Service {Cluster}/{Service} not found", clusterName, serviceName);
                return null;
            }
        }
        catch (ClusterNotFoundException)
        {
            _logger.LogWarning("Cluster {Cluster} not found", clusterName);
            return null;
        }

        DescribeTaskDefinitionResponse? describeTaskDefinitionResponse = await _ecsClient.DescribeTaskDefinitionAsync(
            new DescribeTaskDefinitionRequest
            {
                TaskDefinition = service.TaskDefinition
            }, cancellationToken);

        return describeTaskDefinitionResponse.TaskDefinition;
    }

    private static RegisterTaskDefinitionRequest CreateRegisterTaskDefinitionRequest(TaskDefinition taskDefinition)
    {
        return new RegisterTaskDefinitionRequest
        {
            ContainerDefinitions = taskDefinition.ContainerDefinitions,
            Cpu = taskDefinition.Cpu,
            EphemeralStorage = taskDefinition.EphemeralStorage,
            ExecutionRoleArn = taskDefinition.ExecutionRoleArn,
            Family = taskDefinition.Family,
            InferenceAccelerators = taskDefinition.InferenceAccelerators,
            IpcMode = taskDefinition.IpcMode,
            Memory = taskDefinition.Memory,
            NetworkMode = taskDefinition.NetworkMode,
            PidMode = taskDefinition.PidMode,
            PlacementConstraints = taskDefinition.PlacementConstraints,
            ProxyConfiguration = taskDefinition.ProxyConfiguration,
            RequiresCompatibilities = taskDefinition.RequiresCompatibilities,
            RuntimePlatform = taskDefinition.RuntimePlatform,
            TaskRoleArn = taskDefinition.TaskRoleArn,
            Volumes = taskDefinition.Volumes
        };
    }

    
}