using Delgato.Dashboard.Web;
using Delgato.Dashboard.Web.Components;
using Delgato.Agents;
using Delgato.Core.Abstractions;
using Delgato.Orchestration;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

// Register Delgato services
// Find solution root by looking for .slnx file, then navigate to agents folder
string agentDirectory;
var currentDir = AppContext.BaseDirectory;
var solutionRoot = currentDir;

// Walk up the directory tree to find the solution root (where Delgato.slnx is)
while (solutionRoot != null && !File.Exists(Path.Combine(solutionRoot, "Delgato.slnx")))
{
    var parent = Directory.GetParent(solutionRoot);
    solutionRoot = parent?.FullName;
}

if (solutionRoot != null)
{
    agentDirectory = Path.Combine(solutionRoot, "agents");
    Console.WriteLine($"[Delgato] Found solution root: {solutionRoot}");
}
else
{
    // Fallback to calculation method
    agentDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "agents"));
    Console.WriteLine($"[Delgato] Using calculated path (solution root not found)");
}

// Add to configuration
builder.Configuration["AgentDirectory"] = agentDirectory;

// Log the directory path during configuration
Console.WriteLine($"[Delgato] Agent directory configured: {agentDirectory}");
Console.WriteLine($"[Delgato] Directory exists: {Directory.Exists(agentDirectory)}");
if (Directory.Exists(agentDirectory))
{
    var files = Directory.GetFiles(agentDirectory, "*.yaml");
    Console.WriteLine($"[Delgato] Found {files.Length} YAML files");
    foreach (var file in files)
    {
        Console.WriteLine($"[Delgato]   - {Path.GetFileName(file)}");
    }
}

// Create registry without initializing yet
builder.Services.AddSingleton<IAgentRegistry>(sp =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger<Program>();
    
    logger.LogInformation("Creating Agent Registry for directory: {AgentDirectory}", agentDirectory);
    
    if (!Directory.Exists(agentDirectory))
    {
        logger.LogWarning("Agent directory does not exist, creating: {AgentDirectory}", agentDirectory);
        Directory.CreateDirectory(agentDirectory);
    }
    
    var registry = new FileBasedAgentRegistry(agentDirectory);
    
    // Don't initialize here - will be initialized on first use
    logger.LogInformation("Agent Registry created (will initialize on first use)");
    
    return registry;
});

builder.Services.AddSingleton<IAgentFactory, SemanticKernelAgentFactory>();
builder.Services.AddSingleton<IOrchestrator, SwarmOrchestrator>();

// Agent management service for CRUD operations
builder.Services.AddScoped<Delgato.Dashboard.Web.Services.AgentManagementService>();

// Agent execution service for running agents
builder.Services.AddScoped<Delgato.Dashboard.Web.Services.AgentExecutionService>();

builder.Services.AddHttpClient<WeatherApiClient>(client =>
    {
        // This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
        // Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
        client.BaseAddress = new("https+http://apiservice");
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
