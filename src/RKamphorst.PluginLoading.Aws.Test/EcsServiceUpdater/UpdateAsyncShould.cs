using System.Linq.Expressions;
using System.Net;
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

    private static readonly DateTimeOffset ProviderTimestamp = DateTimeOffset.Parse("1982-03-18T02:55:15Z");
    
    public UpdateAsyncShould()
    {
        _ecsClientMock = new Mock<IAmazonECS>(MockBehavior.Strict);
        _timestampProviderMock = new Mock<IPluginLibraryTimestampProvider>(MockBehavior.Strict);
        _timestampProviderMock
            .Setup(m => m.GetTimestampAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderTimestamp);

    }

    [Fact]
    public async Task UpdateOneService()
    {
        SetupDescribeServicesAsync(new Dictionary<string, string>
        {
            ["service"] = "taskDefinition:3"
        });
        SetupDescribeTaskDefinitionAsync(new Dictionary<string, Dictionary<string, string>>
        {
            ["container"] = new()
        });
        SetupRegisterTaskDefinitionAsync(
            req =>
                req.ContainerDefinitions.Count == 1
                && req.ContainerDefinitions[0].Environment.Count == 1 
                && req.ContainerDefinitions[0].Environment[0].Name == Constants.DefaultVersionAtDateEnvironmentVariable
                && DateTimeOffset.Parse(req.ContainerDefinitions[0].Environment[0].Value) == ProviderTimestamp, 
            15
        );
        SetupDeRegisterTaskDefinitionAsync("taskDefinition:3");
        
        SetupUpdateServiceAsync(req =>
            req.Cluster == "cluster" && req.Service == "service" && req.TaskDefinition == "taskDefinition:15");
        
        var sut = CreateSystemUnderTest((Cluster: "cluster", Service: "service"));

        await sut.UpdateAsync(CancellationToken.None);

        _ecsClientMock.VerifyAll();
    }
    
    [Fact]
    public async Task UseCurrentTimeIfProviderTimestampIsNull()
    {
        _timestampProviderMock
            .Setup(m => m.GetTimestampAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTimeOffset?)null);
        var currentTime = DateTimeOffset.Parse("2022-01-01T10:03:00Z");
        SetupDescribeServicesAsync(new Dictionary<string, string>
        {
            ["service"] = "taskDefinition:3"
        });
        SetupDescribeTaskDefinitionAsync(new Dictionary<string, Dictionary<string, string>>
        {
            ["container"] = new()
        });
        SetupRegisterTaskDefinitionAsync(
            req =>
                req.ContainerDefinitions.Count == 1
                && req.ContainerDefinitions[0].Environment.Count == 1 
                && req.ContainerDefinitions[0].Environment[0].Name == Constants.DefaultVersionAtDateEnvironmentVariable
                && DateTimeOffset.Parse(req.ContainerDefinitions[0].Environment[0].Value) == currentTime, 
            15
        );
        SetupDeRegisterTaskDefinitionAsync("taskDefinition:3");
        SetupUpdateServiceAsync(req =>
            req.Cluster == "cluster" && req.Service == "service" && req.TaskDefinition == "taskDefinition:15");
        
        var sut = CreateSystemUnderTest((Cluster: "cluster", Service: "service"));
        sut.GetNowTime = () => currentTime;

        await sut.UpdateAsync(CancellationToken.None);

        _ecsClientMock.VerifyAll();
    }

    [Fact]
    public async Task UpdateOneServiceWithMultiContainerTaskDefinition()
    {
        SetupDescribeServicesAsync(new Dictionary<string, string>
        {
            ["service"] = "taskDefinition:3"
        });
        SetupDescribeTaskDefinitionAsync(new Dictionary<string, Dictionary<string, string>>
        {
            ["container0"] = new(),
            ["container1"] = new(),
            ["container2"] = new()
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
        SetupDeRegisterTaskDefinitionAsync("taskDefinition:3");
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
            ["service1"] = "taskDefinition1:11",
            ["service2"] = "taskDefinition2:22",
            ["service3"] = "taskDefinition3:33"
        });
        SetupDescribeTaskDefinitionAsync(new Dictionary<string, Dictionary<string, string>>
        {
            ["container"] = new()
        });
        SetupRegisterTaskDefinitionAsync(req => req.Family == "taskDefinition1", 15);
        SetupRegisterTaskDefinitionAsync(req => req.Family == "taskDefinition2", 16);
        SetupRegisterTaskDefinitionAsync(req => req.Family == "taskDefinition3", 17);
        SetupDeRegisterTaskDefinitionAsync("taskDefinition1:11");
        SetupDeRegisterTaskDefinitionAsync("taskDefinition2:22");
        SetupDeRegisterTaskDefinitionAsync("taskDefinition3:33");
        
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
    public async Task UpdateSameTaskDefinitionOnlyOnce()
    {
        SetupDescribeServicesAsync(new Dictionary<string, string>
        {
            ["service1"] = "taskDefinition:1",
            ["service2"] = "taskDefinition:1",
            ["service3"] = "taskDefinition:1"
        });
        SetupDescribeTaskDefinitionAsync(new Dictionary<string, Dictionary<string, string>>
        {
            ["container"] = new()
        });
        SetupRegisterTaskDefinitionAsync(req => req.Family == "taskDefinition", 15);
        SetupDeRegisterTaskDefinitionAsync("taskDefinition:1");
        SetupUpdateServiceAsync(req =>
            req.Cluster == "cluster" && req.Service == "service1" && req.TaskDefinition == "taskDefinition:15");
        SetupUpdateServiceAsync(req =>
            req.Cluster == "cluster" && req.Service == "service2" && req.TaskDefinition == "taskDefinition:15");
        SetupUpdateServiceAsync(req =>
            req.Cluster == "cluster" && req.Service == "service3" && req.TaskDefinition == "taskDefinition:15");
        
        var sut = CreateSystemUnderTest(
            (Cluster: "cluster", Service: "service1"), 
            (Cluster: "cluster", Service: "service2"), 
            (Cluster: "cluster", Service: "service3")
        );

        await sut.UpdateAsync(CancellationToken.None);

        _ecsClientMock.VerifyAll();
        _ecsClientMock.Verify(
            m => m.RegisterTaskDefinitionAsync(It.IsAny<RegisterTaskDefinitionRequest>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        _ecsClientMock.Verify(
            m => m.DeregisterTaskDefinitionAsync(It.IsAny<DeregisterTaskDefinitionRequest>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task NotCrashIfOptionsIncomplete()
    {
        SetupDescribeServicesAsync(new Dictionary<string, string>
        {
            ["service1"] = "taskDefinition1:1",
            ["service2"] = "taskDefinition2:2",
            ["service3"] = "taskDefinition3:3"
        });
        SetupDescribeTaskDefinitionAsync(new Dictionary<string, Dictionary<string, string>>
        {
            ["container"] = new()
        });
        SetupRegisterTaskDefinitionAsync(req => req.Family == "taskDefinition1", 15);
        SetupRegisterTaskDefinitionAsync(req => req.Family == "taskDefinition2", 16);
        SetupRegisterTaskDefinitionAsync(req => req.Family == "taskDefinition3", 17);
        SetupDeRegisterTaskDefinitionAsync("taskDefinition1:1");
        SetupDeRegisterTaskDefinitionAsync("taskDefinition2:2");
        SetupDeRegisterTaskDefinitionAsync("taskDefinition3:3");
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
            ["service"] = "taskDefinition:666"
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
        SetupDeRegisterTaskDefinitionAsync("taskDefinition:666");

        SetupUpdateServiceAsync(req =>
            req.Cluster == "cluster" && req.Service == "service" && req.TaskDefinition == "taskDefinition:15");
        
        var sut = CreateSystemUnderTest((Cluster: "cluster", Service: "service"));

        await sut.UpdateAsync(CancellationToken.None);

        _ecsClientMock.VerifyAll();
    }
    
    [Fact]
    public async Task NotPerformUpdateIfNoContainerEnvironmentChanged()
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
                [Constants.DefaultVersionAtDateEnvironmentVariable] = ProviderTimestamp.ToString("O")
            }
        });
        
        var sut = CreateSystemUnderTest((Cluster: "cluster", Service: "service"));

        await sut.UpdateAsync(CancellationToken.None);

        _ecsClientMock.VerifyAll();
        _ecsClientMock.VerifyNoOtherCalls();
    }
    
    [Fact]
    public async Task NotPerformUpdateIfLibrarySourceEmptyAndVersionAtDateSet()
    {
        _timestampProviderMock
            .Setup(m => m.GetTimestampAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTimeOffset?)null);
        
        SetupDescribeServicesAsync(new Dictionary<string, string>
        {
            ["service"] = "taskDefinition"
        });
        SetupDescribeTaskDefinitionAsync(new Dictionary<string, Dictionary<string, string>>
        {
            ["container"] = new()
            {
                ["ENVIRONMENT_VARIABLE"] = "OLD_VALUE",
                [Constants.DefaultVersionAtDateEnvironmentVariable] = "2019-01-01T00:01:33Z"
            }
        });
        
        var sut = CreateSystemUnderTest((Cluster: "cluster", Service: "service"));

        await sut.UpdateAsync(CancellationToken.None);

        _ecsClientMock.VerifyAll();
        _ecsClientMock.VerifyNoOtherCalls();
    }
    
    [Fact]
    public async Task PerformUpdateIfLibrarySourceEmptyAndVersionAtDateNotSet()
    {
        _timestampProviderMock
            .Setup(m => m.GetTimestampAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTimeOffset?)null);
        var currentTime = DateTimeOffset.Parse("2022-01-01T10:03:00Z");
        SetupDescribeServicesAsync(new Dictionary<string, string>
        {
            ["service"] = "taskDefinition:1"
        });
        SetupDescribeTaskDefinitionAsync(new Dictionary<string, Dictionary<string, string>>
        {
            ["container"] = new()
        });
        SetupRegisterTaskDefinitionAsync(
            req =>
                req.ContainerDefinitions.Count == 1
                && req.ContainerDefinitions[0].Environment.Count == 1 
                && req.ContainerDefinitions[0].Environment[0].Name == Constants.DefaultVersionAtDateEnvironmentVariable
                && DateTimeOffset.Parse(req.ContainerDefinitions[0].Environment[0].Value) == currentTime, 
            15
        );
        SetupDeRegisterTaskDefinitionAsync("taskDefinition:1");

        SetupUpdateServiceAsync(req =>
            req.Cluster == "cluster" && req.Service == "service" && req.TaskDefinition == "taskDefinition:15");
        
        var sut = CreateSystemUnderTest((Cluster: "cluster", Service: "service"));
        sut.GetNowTime = () => currentTime;
        
        await sut.UpdateAsync(CancellationToken.None);

        _ecsClientMock.VerifyAll();
    }
    
    [Fact]
    public async Task NotThrowExceptionIfServiceNotFound()
    {
        SetupDescribeServicesAsync(new Dictionary<string, string>
        {
        });
        
        var sut = CreateSystemUnderTest((Cluster: "cluster", Service: "service"));

        await sut.UpdateAsync(CancellationToken.None);

        _ecsClientMock.VerifyAll();
    }
    
    [Fact]
    public async Task NotThrowExceptionIfClusterNotFound()
    {
        _ecsClientMock
            .Setup(
                m => m.DescribeServicesAsync(It.IsAny<DescribeServicesRequest>(), It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(new ClusterNotFoundException("cluster not found"));

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
                    }).ToArray()
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
                        ServiceName = n, TaskDefinition = serviceTaskDefinitions.TryGetValue(n, out var td) ? td : null
                    })
                        .Where(s => s.TaskDefinition != null)
                        .ToList()
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

    private void SetupDeRegisterTaskDefinitionAsync(string taskDefinition)
    {
        SetupDeRegisterTaskDefinitionAsync(rq => rq.TaskDefinition == taskDefinition);
    }
    
    private void SetupDeRegisterTaskDefinitionAsync(
        Expression<Func<DeregisterTaskDefinitionRequest, bool>> requestExpression)
    {
        _ecsClientMock
            .Setup(m => m.DeregisterTaskDefinitionAsync(It.Is(requestExpression),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (DeregisterTaskDefinitionRequest rq, CancellationToken _) => new DeregisterTaskDefinitionResponse()
            )
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