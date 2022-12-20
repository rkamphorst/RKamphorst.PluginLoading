using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.S3Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RKamphorst.PluginLoading.Aws;
using RKamphorst.PluginLoading.Aws.DependencyInjection;
using Task = System.Threading.Tasks.Task;

// determine environment from DOTNET_ENVIRONMENT environment variable; default to Master
var environment =
    Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Master";

// read configuration from appsettings.json, appsettings.(Development|Master|Production).json,
// and environment variables; configuration from later sources overrides earlier sources.
var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables("HANDLER_")
    .Build();

// configure services
var serviceProvider = new ServiceCollection()
    
    // add support for logging
    .AddLogging(b => b.AddConsole())
    
    // add plugin loading from AWS services;
    // this includes the IEcsServiceUpdater implementation
    .AddPluginLoadingFromAws(options => configuration.Bind(options))
    .BuildServiceProvider();

if (environment.ToUpperInvariant().StartsWith("DEV"))
{
    // run handler once if in development mode
    await HandleS3EventAsync();
}
else
{
    // otherwise, bootstrap the lambda and hook up the handler
    await LambdaBootstrapBuilder.Create(
            (S3Event _, ILambdaContext _) => HandleS3EventAsync(),
            new DefaultLambdaJsonSerializer()
            )
        .Build()
        .RunAsync();
}

// the lambda handler function
async Task HandleS3EventAsync()
{
    using var scope = serviceProvider.CreateScope();

    var updater = serviceProvider.GetRequiredService<IEcsServiceUpdater>();

    await updater.UpdateAsync(default);
}