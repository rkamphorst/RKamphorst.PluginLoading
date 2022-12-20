using System.Linq.Expressions;
using Amazon.ECS;
using Amazon.ECS.Model;
using Microsoft.Extensions.Logging;
using Moq;
using RKamphorst.PluginLoading.Aws.Options;
using Xunit;
using KeyValuePair = Amazon.ECS.Model.KeyValuePair;
using Task = System.Threading.Tasks.Task;

namespace RKamphorst.PluginLoading.Aws.Test.EcsServiceUpdater;

using EcsServiceUpdater = Aws.EcsServiceUpdater;

public class UpdateAsyncShould
{
    private readonly Mock<IAmazonECS> _ecsClientMock;
    private readonly Mock<IPluginLibraryTimestampProvider> _timestampProviderMock;

    public UpdateAsyncShould()
    {
        _ecsClientMock = new Mock<IAmazonECS>(MockBehavior.Strict);
        _timestampProviderMock = new Mock<IPluginLibraryTimestampProvider>(MockBehavior.Strict);
        _timestampProviderMock
            .Setup(m => m.GetTimestampAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTimeOffset.Parse("1982-03-18T02:55:15Z"));

    }

    [Fact]
    public async Task UpdateOneService()
    {
        SetupDescribeServicesAsync(new Dictionary<string, string>
        {
            ["service"] = "taskDefinition"
        });
        SetupDescribeTaskDefinitionAsync(new Dictionary<string, Dictionary<string, string>>
        {
            ["container"] = new() {}
        });
        SetupRegisterTaskDefinitionAsync(
            req =>
                req.ContainerDefinitions.Count == 1
                && req.ContainerDefinitions[0].Environment.Count == 1 
                && req.ContainerDefinitions[0].Environment[0].Name == Constants.DefaultVersionAtDateEnvironmentVariable,
            15
        );
        SetupUpdateServiceAsync(req =>
            req.Cluster == "cluster" && req.Service == "service" && req.TaskDefinition == "taskDefinition:15");
        
        var sut = CreateSystemUnderTest((Cluster: "cluster", Service: "service"));

        await sut.UpdateAsync(CancellationToken.None);

        _ecsClientMock.VerifyAll();
    }

    [Fact]
    public async Task UpdateOneServiceWithMultiContainerTaskDefinition()
    {
        SetupDescribeServicesAsync(new Dictionary<string, string>
        {
            ["service"] = "taskDefinition"
        });
        SetupDescribeTaskDefinitionAsync(new Dictionary<string, Dictionary<string, string>>
        {
            ["container0"] = new() { },
            ["container1"] = new() { },
            ["container2"] = new() { }
        });
        SetupRegisterTaskDefinitionAsync(
            req =>
                req.ContainerDefinitions.Count == 3
                
                && req.ContainerDefinitions[0].Environment.Count == 1
                && req.ContainerDefinitions[0].Environment[0].Name == Constants.DefaultVersionAtDateEnvironmentVariable
                && req.ContainerDefinitions[1].Environment.Count == 1
                && req.ContainerDefinitions[1].Environment[0].Name == Constants.DefaultVersionAtDateEnvironmentVariable
                && req.ContainerDefinitions[2].Environment.Count == 1 
                && req.ContainerDefinitions[2].Environment[0].Name == Constants.DefaultVersionAtDateEnvironmentVariable,
            15
        );
        SetupUpdateServiceAsync(req =>
            req.Cluster == "cluster" && req.Service == "service" && req.TaskDefinition == "taskDefinition:15");

        var sut = CreateSystemUnderTest((Cluster: "cluster", Service: "service"));

        await sut.UpdateAsync(CancellationToken.None);

        _ecsClientMock.VerifyAll();
    }

    [Fact]
    public async Task UpdateManyServices()
    {
        SetupDescribeServicesAsync(new Dictionary<string, string>
        {
            ["service1"] = "taskDefinition1",
            ["service2"] = "taskDefinition2",
            ["service3"] = "taskDefinition3"
        });
        SetupDescribeTaskDefinitionAsync(new Dictionary<string, Dictionary<string, string>>
        {
            ["container"] = new() {}
        });
        SetupRegisterTaskDefinitionAsync(req => req.Family == "taskDefinition1", 15);
        SetupRegisterTaskDefinitionAsync(req => req.Family == "taskDefinition2", 16);
        SetupRegisterTaskDefinitionAsync(req => req.Family == "taskDefinition3", 17);

        SetupUpdateServiceAsync(req =>
            req.Cluster == "cluster" && req.Service == "service1" && req.TaskDefinition == "taskDefinition1:15");
        SetupUpdateServiceAsync(req =>
            req.Cluster == "cluster" && req.Service == "service2" && req.TaskDefinition == "taskDefinition2:16");
        SetupUpdateServiceAsync(req =>
            req.Cluster == "cluster" && req.Service == "service3" && req.TaskDefinition == "taskDefinition3:17");
        
        var sut = CreateSystemUnderTest(
            (Cluster: "cluster", Service: "service1"), 
            (Cluster: "cluster", Service: "service2"), 
            (Cluster: "cluster", Service: "service3")
        );

        await sut.UpdateAsync(CancellationToken.None);

        _ecsClientMock.VerifyAll();
    }

    [Fact]
    public async Task NotCrashIfOptionsIncomplete()
    {
        SetupDescribeServicesAsync(new Dictionary<string, string>
        {
            ["service1"] = "taskDefinition1",
            ["service2"] = "taskDefinition2",
            ["service3"] = "taskDefinition3"
        });
        SetupDescribeTaskDefinitionAsync(new Dictionary<string, Dictionary<string, string>>
        {
            ["container"] = new() {}
        });
        SetupRegisterTaskDefinitionAsync(req => req.Family == "taskDefinition1", 15);
        SetupRegisterTaskDefinitionAsync(req => req.Family == "taskDefinition2", 16);
        SetupRegisterTaskDefinitionAsync(req => req.Family == "taskDefinition3", 17);

        SetupUpdateServiceAsync(req =>
            req.Cluster == "cluster" && req.Service == "service1" && req.TaskDefinition == "taskDefinition1:15");
        SetupUpdateServiceAsync(req =>
            req.Cluster == "cluster" && req.Service == "service2" && req.TaskDefinition == "taskDefinition2:16");
        SetupUpdateServiceAsync(req =>
            req.Cluster == "cluster" && req.Service == "service3" && req.TaskDefinition == "taskDefinition3:17");
        
        var sut = CreateSystemUnderTest(
            (Cluster: "cluster", Service: "service1"), 
            (Cluster: "cluster", Service: "service2"), 
            (Cluster: "cluster", Service: "service3"),
            (Cluster: "cluster2", Service: null),
            (Cluster: null, Service: "serviceX")
        );

        await sut.UpdateAsync(CancellationToken.None);

        _ecsClientMock.VerifyAll();
    }

    [Fact]
    public async Task ReplaceEnvironmentVariableIfPresent()
    {
        SetupDescribeServicesAsync(new Dictionary<string, string>
        {
            ["service"] = "taskDefinition"
        });
        SetupDescribeTaskDefinitionAsync(new Dictionary<string, Dictionary<string, string>>
        {
            ["container"] = new()
            {
                ["ENVIRONMENT_VARIABLE"] = "OLD_VALUE",
                [Constants.DefaultVersionAtDateEnvironmentVariable] = "OLD_VALUE"
            }
        });
        SetupRegisterTaskDefinitionAsync(
            req =>
                req.ContainerDefinitions.Count == 1
                && req.ContainerDefinitions[0].Environment.Count == 2
                && req.ContainerDefinitions[0].Environment.Single(
                    kvp => kvp.Name == "ENVIRONMENT_VARIABLE"
                ).Value == "OLD_VALUE"
                && req.ContainerDefinitions[0].Environment
                    .Single(kvp => kvp.Name == Constants.DefaultVersionAtDateEnvironmentVariable
                ).Value != "OLD_VALUE",
            15
        );
        SetupUpdateServiceAsync(req =>
            req.Cluster == "cluster" && req.Service == "service" && req.TaskDefinition == "taskDefinition:15");
        
        var sut = CreateSystemUnderTest((Cluster: "cluster", Service: "service"));

        await sut.UpdateAsync(CancellationToken.None);

        _ecsClientMock.VerifyAll();
    }
    
    private EcsServiceUpdater CreateSystemUnderTest(params (string? Cluster, string? Service)[] servicesAndClusters)
    {
        return new EcsServiceUpdater(_ecsClientMock.Object, new EcsServiceUpdaterOptions
            {
                Services =
                    servicesAndClusters.Select(sac => new ServiceAndCluster
                    {
                        Cluster = sac.Cluster,
                        Service = sac.Service,
                    }).ToArray(),
                DelayMillis = 0
            },
            _timestampProviderMock.Object,
            Mock.Of<ILogger<EcsServiceUpdater>>());
    }
    
    private void SetupDescribeServicesAsync(Dictionary<string,string>serviceTaskDefinitions)
    {
        _ecsClientMock
            .Setup(m => m.DescribeServicesAsync(It.IsAny<DescribeServicesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DescribeServicesRequest rq, CancellationToken _)
                => new DescribeServicesResponse
                {
                    Services = rq.Services.Select(n => new Service
                    {
                        ServiceName = n, TaskDefinition = serviceTaskDefinitions[n]
                    }).ToList()
                })
            .Verifiable();
    }

    private void SetupDescribeTaskDefinitionAsync(Dictionary<string, Dictionary<string, string>>? containerEnvironments = null)
    {
        _ecsClientMock
            .Setup(m => m.DescribeTaskDefinitionAsync(It.IsAny<DescribeTaskDefinitionRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((DescribeTaskDefinitionRequest rq, CancellationToken _) => new DescribeTaskDefinitionResponse
            {
                TaskDefinition = CreateTaskDefinition(rq.TaskDefinition, containerEnvironments)
            })
            .Verifiable();
    }

    private void SetupRegisterTaskDefinitionAsync(Expression<Func<RegisterTaskDefinitionRequest, bool>> requestExpression, int returnRevision)
    {
        _ecsClientMock
            .Setup(m => m.RegisterTaskDefinitionAsync(It.Is(requestExpression),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((RegisterTaskDefinitionRequest rq, CancellationToken _) => new RegisterTaskDefinitionResponse
                {
                    TaskDefinition = CreateTaskDefinition($"{rq.Family}:{returnRevision}")
                })
            .Verifiable();
    }

    private void SetupUpdateServiceAsync(Expression<Func<UpdateServiceRequest, bool>> requestExpression)
    {
        _ecsClientMock
            .Setup(m => m.UpdateServiceAsync(It.Is(requestExpression), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateServiceResponse())
            .Verifiable();
    }
    private static TaskDefinition CreateTaskDefinition(string familyAndRevision,
        Dictionary<string, Dictionary<string, string>>? containerEnvironments = null) =>
        new()
        {
            ContainerDefinitions = (containerEnvironments ?? new Dictionary<string, Dictionary<string, string>>())
                .Select(kvp => new ContainerDefinition
                {
                    Name = kvp.Key,
                    Environment = kvp.Value.Select(kvp2 => new KeyValuePair { Name = kvp2.Key, Value = kvp2.Value })
                        .ToList()
                }).ToList(),
            Family = familyAndRevision.Split(":").FirstOrDefault(),
            Revision = int.Parse(familyAndRevision.Split(":").Skip(1).FirstOrDefault() ?? "0")
        };
    

}