using Delgato.Agents;
using Delgato.Configuration;
using Delgato.Core.Abstractions;
using Delgato.Core.Governance;
using Delgato.Governance;
using Delgato.Orchestration;
using Delgato.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Delgato.Cli.Infrastructure;

/// <summary>
/// Extension methods for configuring CLI services.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDelgatoCliServices(this IServiceCollection services)
    {
        // Add logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Warning);
            builder.AddConsole();
        });

        // Add HTTP client
        services.AddHttpClient();

        // Add providers
        services.AddDelgatoProviders(options =>
        {
            options.EnableAnthropic = !string.IsNullOrEmpty(
                Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));
            options.EnableOpenAI = !string.IsNullOrEmpty(
                Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
            options.EnableGemini = !string.IsNullOrEmpty(
                Environment.GetEnvironmentVariable("GOOGLE_API_KEY"));
        });

        // Add agent registry
        services.AddSingleton<IAgentRegistry>(sp =>
        {
            var agentsPath = GetAgentsPath();
            return new FileBasedAgentRegistry(agentsPath);
        });

        // Add agent factory
        services.AddSingleton<IAgentFactory, SemanticKernelAgentFactory>();

        // Add file loader
        services.AddSingleton<AgentFileLoader>();

        // Add orchestrator
        services.AddSingleton<IOrchestrator>(sp =>
        {
            var registry = sp.GetRequiredService<IAgentRegistry>();
            var factory = sp.GetRequiredService<IAgentFactory>();
            var providerRegistry = sp.GetRequiredService<IProviderRegistry>();
            var logger = sp.GetRequiredService<ILogger<TreeOrchestrator>>();
            return new TreeOrchestrator(registry, factory, providerRegistry, logger);
        });

        // Add governance services
        services.AddSingleton<IAuditService, InMemoryAuditService>();
        services.AddSingleton<ICostTracker, InMemoryCostTracker>();

        return services;
    }

    private static string GetAgentsPath()
    {
        // Check for environment variable
        var envPath = Environment.GetEnvironmentVariable("DELGATO_AGENTS_PATH");
        if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
            return envPath;

        // Check for agents directory in current directory
        var currentPath = Path.Combine(Directory.GetCurrentDirectory(), "agents");
        if (Directory.Exists(currentPath))
            return currentPath;

        // Use user's home directory
        var homePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".delgato", "agents");

        if (!Directory.Exists(homePath))
            Directory.CreateDirectory(homePath);

        return homePath;
    }
}
