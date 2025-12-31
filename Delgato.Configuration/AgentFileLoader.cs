using Delgato.Configuration.Schema;
using Delgato.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Delgato.Configuration;

/// <summary>
/// Loads agent definitions from YAML/JSON files.
/// </summary>
public sealed class AgentFileLoader
{
    private readonly IDeserializer _yamlDeserializer;
    private readonly IDeserializer _jsonDeserializer;

    public AgentFileLoader()
    {
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        
        _jsonDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }

    public async ValueTask<IReadOnlyList<AgentDefinition>> LoadFromFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        AgentFileSchema schema = extension switch
        {
            ".yaml" or ".yml" => _yamlDeserializer.Deserialize<AgentFileSchema>(content),
            ".json" => _jsonDeserializer.Deserialize<AgentFileSchema>(content),
            _ => throw new NotSupportedException($"File extension '{extension}' is not supported.")
        };

        return schema.Agents.Select(MapToDefinition).ToList();
    }

    public async ValueTask<IReadOnlyList<AgentDefinition>> LoadFromDirectoryAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        var files = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                       f.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
                       f.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

        var definitions = new List<AgentDefinition>();
        
        foreach (var file in files)
        {
            var fileDefs = await LoadFromFileAsync(file, cancellationToken);
            definitions.AddRange(fileDefs);
        }

        return definitions;
    }

    private static AgentDefinition MapToDefinition(AgentSchema schema)
    {
        return new AgentDefinition
        {
            Id = new AgentId(schema.Id),
            Name = schema.Name,
            Description = schema.Description,
            Prompt = schema.Prompt,
            PromptRef = schema.PromptRef != null
                ? new PromptReference
                {
                    Source = schema.PromptRef.Source,
                    Value = schema.PromptRef.Value
                }
                : null,
            Model = schema.Model != null
                ? new ModelConfiguration
                {
                    Provider = schema.Model.Provider,
                    ModelId = schema.Model.ModelId,
                    Temperature = schema.Model.Temperature,
                    MaxTokens = schema.Model.MaxTokens,
                    Parameters = schema.Model.Parameters
                }
                : null,
            Capabilities = schema.Capabilities,
            AllowedTools = schema.AllowedTools,
            Budget = schema.Budget != null
                ? new Budget
                {
                    MaxTokens = schema.Budget.MaxTokens,
                    MaxToolCalls = schema.Budget.MaxToolCalls,
                    MaxDepth = schema.Budget.MaxDepth,
                    MaxDuration = schema.Budget.MaxDurationSeconds.HasValue
                        ? TimeSpan.FromSeconds(schema.Budget.MaxDurationSeconds.Value)
                        : null,
                    MaxCost = schema.Budget.MaxCost
                }
                : null,
            Policy = schema.Policy != null
                ? new PolicyConfiguration
                {
                    DeniedTools = schema.Policy.DeniedTools,
                    RequiredCapabilities = schema.Policy.RequiredCapabilities,
                    AuditAllToolCalls = schema.Policy.AuditAllToolCalls,
                    AllowRecursion = schema.Policy.AllowRecursion
                }
                : null,
            IsOrchestrator = schema.IsOrchestrator,
            Metadata = schema.Metadata
        };
    }
}

