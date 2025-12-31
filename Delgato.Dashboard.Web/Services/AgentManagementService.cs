using Delgato.Core;
using Delgato.Core.Abstractions;
using Delgato.Configuration;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Delgato.Dashboard.Web.Services;

/// <summary>
/// Service for managing agents through the dashboard - provides CRUD operations.
/// </summary>
public class AgentManagementService
{
    private readonly IAgentRegistry _registry;
    private readonly string _agentDirectory;
    private readonly ILogger<AgentManagementService> _logger;
    private readonly ISerializer _yamlSerializer;
    private readonly IDeserializer _yamlDeserializer;

    public AgentManagementService(
        IAgentRegistry registry,
        IConfiguration configuration,
        ILogger<AgentManagementService> logger)
    {
        _registry = registry;
        _logger = logger;
        
        // Try to get agent directory from configuration, fallback to calculating
        _agentDirectory = configuration["AgentDirectory"] 
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "agents"));
        
        _logger.LogInformation("AgentManagementService using directory: {AgentDirectory}", _agentDirectory);
        
        // Initialize YAML serialization
        _yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }

    public async Task<IReadOnlyList<AgentDefinition>> GetAllAgentsAsync()
    {
        return await _registry.GetAllDefinitionsAsync();
    }

    public async Task<AgentDefinition?> GetAgentAsync(string agentId)
    {
        return await _registry.GetDefinitionAsync(new AgentId(agentId));
    }

    public async Task<AgentDefinition> CreateAgentAsync(AgentDefinition definition)
    {
        _logger.LogInformation("Creating agent: {AgentId}", definition.Id);

        // Save to YAML file
        var filePath = Path.Combine(_agentDirectory, $"{definition.Id.Value}.yaml");
        await SaveAgentToFileAsync(definition, filePath);

        // Register with registry
        await _registry.RegisterAsync(definition);

        return definition;
    }

    public async Task<AgentDefinition> UpdateAgentAsync(AgentDefinition definition)
    {
        _logger.LogInformation("Updating agent: {AgentId}", definition.Id);

        // Update YAML file
        var filePath = Path.Combine(_agentDirectory, $"{definition.Id.Value}.yaml");
        await SaveAgentToFileAsync(definition, filePath);

        // Update in registry
        await _registry.UnregisterAsync(definition.Id);
        await _registry.RegisterAsync(definition);

        return definition;
    }

    public async Task DeleteAgentAsync(string agentId)
    {
        _logger.LogInformation("Deleting agent: {AgentId}", agentId);

        // Delete YAML file
        var filePath = Path.Combine(_agentDirectory, $"{agentId}.yaml");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        // Unregister from registry
        await _registry.UnregisterAsync(new AgentId(agentId));
    }

    public async Task<string> GetAgentYamlAsync(string agentId)
    {
        var filePath = Path.Combine(_agentDirectory, $"{agentId}.yaml");
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Agent file not found: {agentId}");
        }

        return await File.ReadAllTextAsync(filePath);
    }

    public async Task SaveAgentFromYamlAsync(string agentId, string yamlContent)
    {
        var filePath = Path.Combine(_agentDirectory, $"{agentId}.yaml");
        await File.WriteAllTextAsync(filePath, yamlContent);

        // Reload from file
        var loader = new AgentFileLoader();
        var definitions = await loader.LoadFromFileAsync(filePath);
        
        if (definitions.Count > 0)
        {
            var definition = definitions[0];
            await _registry.UnregisterAsync(definition.Id);
            await _registry.RegisterAsync(definition);
        }
    }

    private async Task SaveAgentToFileAsync(AgentDefinition definition, string filePath)
    {
        // Convert to YAML schema format
        var schema = new Configuration.Schema.AgentFileSchema
        {
            Version = "1.0",
            Agents = new List<Configuration.Schema.AgentSchema>
            {
                new Configuration.Schema.AgentSchema
                {
                    Id = definition.Id.Value,
                    Name = definition.Name,
                    Description = definition.Description,
                    Prompt = definition.Prompt,
                    PromptRef = definition.PromptRef != null ? new Configuration.Schema.PromptRefSchema
                    {
                        Source = definition.PromptRef.Source,
                        Value = definition.PromptRef.Value
                    } : null,
                    Model = definition.Model != null ? new Configuration.Schema.ModelSchema
                    {
                        Provider = definition.Model.Provider,
                        ModelId = definition.Model.ModelId,
                        Temperature = definition.Model.Temperature,
                        MaxTokens = definition.Model.MaxTokens
                    } : null,
                    Capabilities = definition.Capabilities.ToList(),
                    AllowedTools = definition.AllowedTools.ToList(),
                    Budget = definition.Budget != null ? new Configuration.Schema.BudgetSchema
                    {
                        MaxTokens = definition.Budget.MaxTokens,
                        MaxToolCalls = definition.Budget.MaxToolCalls,
                        MaxDepth = definition.Budget.MaxDepth,
                        MaxDurationSeconds = (int?)definition.Budget.MaxDuration?.TotalSeconds,
                        MaxCost = definition.Budget.MaxCost
                    } : null,
                    IsOrchestrator = definition.IsOrchestrator
                }
            }
        };

        var yaml = _yamlSerializer.Serialize(schema);
        await File.WriteAllTextAsync(filePath, yaml);
    }

    public string GetAgentDirectory() => _agentDirectory;
}

