using Delgato.Core;
using Delgato.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;

namespace Delgato.Agents;

/// <summary>
/// Factory for creating Semantic Kernel-based agents.
/// </summary>
public sealed class SemanticKernelAgentFactory : IAgentFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<SemanticKernelAgentFactory> _logger;

    public SemanticKernelAgentFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = _loggerFactory.CreateLogger<SemanticKernelAgentFactory>();
    }

    public ValueTask<IAgent> CreateAgentAsync(
        AgentDefinition definition,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating agent: {AgentId} ({AgentName})", definition.Id, definition.Name);

        var kernelBuilder = Kernel.CreateBuilder();

        // Configure model provider
        if (definition.Model != null)
        {
            ConfigureModelProvider(kernelBuilder, definition.Model);
        }

        var kernel = kernelBuilder.Build();

        var agent = new SemanticKernelAgent(
            definition.Id,
            definition,
            kernel,
            _loggerFactory.CreateLogger<SemanticKernelAgent>());

        return ValueTask.FromResult<IAgent>(agent);
    }

    private void ConfigureModelProvider(IKernelBuilder builder, ModelConfiguration model)
    {
        _logger.LogInformation("Configuring model provider: {Provider}/{ModelId}", 
            model.Provider, model.ModelId);

        switch (model.Provider.ToLowerInvariant())
        {
            case "openai":
                ConfigureOpenAI(builder, model);
                break;
            
            case "azure":
            case "azureopenai":
                ConfigureAzureOpenAI(builder, model);
                break;
            
            case "claude":
            case "anthropic":
                ConfigureClaude(builder, model);
                break;
            
            case "ollama":
                ConfigureOllama(builder, model);
                break;
            
            default:
                _logger.LogWarning("Unsupported model provider: {Provider}, using mock", model.Provider);
                break;
        }
    }

    private void ConfigureOpenAI(IKernelBuilder builder, ModelConfiguration model)
    {
        // In production, read from configuration/environment
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "mock-key";
        
        builder.AddOpenAIChatCompletion(
            modelId: model.ModelId,
            apiKey: apiKey);
    }

    private void ConfigureAzureOpenAI(IKernelBuilder builder, ModelConfiguration model)
    {
        // In production, read from configuration/environment
        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? "https://mock.openai.azure.com";
        var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") ?? "mock-key";
        
        builder.AddAzureOpenAIChatCompletion(
            deploymentName: model.ModelId,
            endpoint: endpoint,
            apiKey: apiKey);
    }

    private void ConfigureClaude(IKernelBuilder builder, ModelConfiguration model)
    {
        // Anthropic Claude configuration
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "mock-key";
        
        _logger.LogInformation("Configuring Claude/Anthropic with model: {ModelId}", model.ModelId);
        
        // Add custom Claude chat completion service
        var claudeService = new ClaudeChatCompletionService(
            apiKey,
            model.ModelId,
            _loggerFactory.CreateLogger<ClaudeChatCompletionService>());
        
        builder.Services.AddSingleton<IChatCompletionService>(claudeService);
    }

    private void ConfigureOllama(IKernelBuilder builder, ModelConfiguration model)
    {
        // Ollama configuration - runs locally by default
        var endpoint = Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT") ?? "http://localhost:11434";
        
        _logger.LogInformation("Configuring Ollama with model: {ModelId} at endpoint: {Endpoint}", 
            model.ModelId, endpoint);
        
        builder.AddOllamaChatCompletion(
            modelId: model.ModelId,
            endpoint: new Uri(endpoint));
    }
}

