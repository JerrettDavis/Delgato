using Delgato.Core.Abstractions;
using Delgato.Providers.Anthropic;
using Delgato.Providers.Google;
using Delgato.Providers.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Delgato.Providers;

/// <summary>
/// Extension methods for registering providers with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the provider registry and all built-in providers.
    /// </summary>
    public static IServiceCollection AddDelgatoProviders(
        this IServiceCollection services,
        Action<ProviderRegistryOptions>? configure = null)
    {
        var options = new ProviderRegistryOptions();
        configure?.Invoke(options);

        // Register the provider registry
        services.AddSingleton<IProviderRegistry>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ProviderRegistry>>();
            var registry = new ProviderRegistry(logger);

            // Register configured providers
            if (options.EnableAnthropic)
            {
                var provider = CreateClaudeProvider(sp, options.AnthropicOptions);
                registry.Register(provider);
            }

            if (options.EnableOpenAI)
            {
                var provider = CreateOpenAIProvider(sp, options.OpenAIOptions);
                registry.Register(provider);
            }

            if (options.EnableGemini)
            {
                var provider = CreateGeminiProvider(sp, options.GeminiOptions);
                registry.Register(provider);
            }

            // Register custom providers
            foreach (var customProvider in options.CustomProviders)
            {
                registry.Register(customProvider(sp));
            }

            return registry;
        });

        // Register HttpClient for providers
        services.AddHttpClient("DelgatoProviders")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All
            });

        return services;
    }

    private static ClaudeProvider CreateClaudeProvider(IServiceProvider sp, ProviderOptions options)
    {
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient("DelgatoProviders");
        var logger = sp.GetRequiredService<ILogger<ClaudeProvider>>();
        return new ClaudeProvider(httpClient, logger, options);
    }

    private static OpenAIProvider CreateOpenAIProvider(IServiceProvider sp, ProviderOptions options)
    {
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient("DelgatoProviders");
        var logger = sp.GetRequiredService<ILogger<OpenAIProvider>>();
        return new OpenAIProvider(httpClient, logger, options);
    }

    private static GeminiProvider CreateGeminiProvider(IServiceProvider sp, ProviderOptions options)
    {
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient("DelgatoProviders");
        var logger = sp.GetRequiredService<ILogger<GeminiProvider>>();
        return new GeminiProvider(httpClient, logger, options);
    }
}

/// <summary>
/// Options for configuring the provider registry.
/// </summary>
public class ProviderRegistryOptions
{
    /// <summary>
    /// Enable Anthropic Claude provider.
    /// </summary>
    public bool EnableAnthropic { get; set; } = true;

    /// <summary>
    /// Options for Anthropic provider.
    /// </summary>
    public ProviderOptions AnthropicOptions { get; set; } = new()
    {
        ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? ""
    };

    /// <summary>
    /// Enable OpenAI provider.
    /// </summary>
    public bool EnableOpenAI { get; set; } = true;

    /// <summary>
    /// Options for OpenAI provider.
    /// </summary>
    public ProviderOptions OpenAIOptions { get; set; } = new()
    {
        ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? ""
    };

    /// <summary>
    /// Enable Google Gemini provider.
    /// </summary>
    public bool EnableGemini { get; set; } = true;

    /// <summary>
    /// Options for Gemini provider.
    /// </summary>
    public ProviderOptions GeminiOptions { get; set; } = new()
    {
        ApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY") ?? ""
    };

    /// <summary>
    /// Custom provider factories.
    /// </summary>
    public List<Func<IServiceProvider, IAgentProvider>> CustomProviders { get; } = new();

    /// <summary>
    /// Adds a custom provider.
    /// </summary>
    public ProviderRegistryOptions AddCustomProvider(Func<IServiceProvider, IAgentProvider> factory)
    {
        CustomProviders.Add(factory);
        return this;
    }
}
