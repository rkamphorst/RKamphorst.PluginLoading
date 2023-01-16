using RKamphorst.PluginLoading.Contract;
using RKamphorst.PluginLoading.DependencyInjection;
using Sample.Api;
using Sample.Contract;


WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables("API_");
builder.Logging.AddConsole();

// download plugins and configure services to use them
await builder.Host.LoadPluginsAsync(RegisterPlugins, logging: builder.Logging);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

void RegisterPlugins(IPluginRegistration registration, ILoggerFactory loggerFactory)
{
    var logger = loggerFactory.CreateLogger<PluginLibrarySource>();
    var pluginLibrarySource = new PluginLibrarySource("Local", logger);
    
    registration.AddPluginsFromSource<ITodoListSource>(pluginLibrarySource);
}