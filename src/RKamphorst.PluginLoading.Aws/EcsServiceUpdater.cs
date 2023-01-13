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

    public async Task UpdateAsync(CancellationToken cancellationToken)
    {
        var versionAtDate = await _timestampProvider.GetTimestampAsync(cancellationToken);

        var clusterServicePairs = _options.Services;

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
                updateTasks.Add(UpdateAsync(clusterName!, serviceName!, versionAtDate, cancellationToken));
            }

            ctr++;
        }

        await Task.WhenAll(updateTasks);
    }
    
    public async Task UpdateAsync(string clusterName, string serviceName, DateTimeOffset versionAtDate, CancellationToken cancellationToken)
    {
        var taskDefinition = await GetCurrentTaskDefinitionAsync(clusterName, serviceName, cancellationToken);
        var taskDefinitionRevision = $"{taskDefinition.Family}:{taskDefinition.Revision}";


        _logger.LogInformation("Updating task definition {TaskDefinitionRevision} to use VersionAtDate {VersionAtDate}",
            taskDefinitionRevision, versionAtDate);

        UpdateContainerDefinitions(taskDefinition.ContainerDefinitions, versionAtDate);

        RegisterTaskDefinitionResponse? registerResponse =
            await _ecsClient.RegisterTaskDefinitionAsync(CreateRegisterTaskDefinitionRequest(taskDefinition),
                cancellationToken);

        var newTaskDefinition = registerResponse.TaskDefinition;
        var newTaskDefinitionRevision = $"{newTaskDefinition.Family}:{newTaskDefinition.Revision}";

        _logger.LogInformation(
            "Updating service {Service} in cluster {Cluster} to use task revision {NewTaskDefinitionRevision}",
            serviceName, clusterName, newTaskDefinitionRevision
        );

        await _ecsClient.UpdateServiceAsync(new UpdateServiceRequest
        {
            Cluster = clusterName,
            Service = serviceName,
            TaskDefinition = newTaskDefinitionRevision
        }, cancellationToken);
    }


    private static void UpdateContainerDefinitions(
        IEnumerable<ContainerDefinition> containerDefinitions, DateTimeOffset versionAtDate)
    {
        foreach (var containerDefinition in containerDefinitions)
        {
            var newEnvironment =
                containerDefinition.Environment
                    .Where(kvp => kvp.Name != Constants.DefaultVersionAtDateEnvironmentVariable)
                    .Concat(new KeyValuePair[]
                    {
                        new() { Name = Constants.DefaultVersionAtDateEnvironmentVariable, Value = versionAtDate.ToString("O") }
                    }).ToList();

            containerDefinition.Environment = newEnvironment;
        }
    }

    private async Task<TaskDefinition> GetCurrentTaskDefinitionAsync(string clusterName, string serviceName, CancellationToken cancellationToken)
    {
        var describeServicesResponse = await _ecsClient.DescribeServicesAsync(new DescribeServicesRequest
        {
            Cluster = clusterName,
            Services = new List<string> { serviceName }
        }, cancellationToken);

        var service = describeServicesResponse.Services.Single();

        var describeTaskDefinitionResponse = await _ecsClient.DescribeTaskDefinitionAsync(
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