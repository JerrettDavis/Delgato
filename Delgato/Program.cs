using Delgato;
using Delgato.Agents;
using Delgato.Core.Abstractions;
using Delgato.Orchestration;
using Delgato.Transports.Http;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
var agentDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "agents");
builder.Services.Configure<SwarmOptions>(options =>
{
    options.AgentDirectory = agentDirectory;
});

// Core services
builder.Services.AddSingleton<IAgentRegistry>(sp =>
{
    var registry = new FileBasedAgentRegistry(agentDirectory);
    // Initialize will be called by the worker
    return registry;
});
builder.Services.AddSingleton<IAgentFactory, SemanticKernelAgentFactory>();
builder.Services.AddSingleton<IOrchestrator, SwarmOrchestrator>();

// Transports
builder.Services.AddSingleton<HttpTransportAdapter>();
builder.Services.AddSingleton<ITransportAdapter>(sp => sp.GetRequiredService<HttpTransportAdapter>());

// Worker
builder.Services.AddHostedService<Worker>();

// TODO: Add web API support in v1 using proper ASP.NET Core integration
var host = builder.Build();
host.Run();
