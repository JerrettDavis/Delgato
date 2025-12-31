using YamlDotNet.Serialization;

namespace Delgato.Configuration.Schema;

/// <summary>
/// YAML/JSON schema for agent definition files.
/// </summary>
public sealed class AgentFileSchema
{
    [YamlMember(Alias = "version")]
    public string Version { get; set; } = "1.0";
    
    [YamlMember(Alias = "agents")]
    public List<AgentSchema> Agents { get; set; } = new();
}

public sealed class AgentSchema
{
    [YamlMember(Alias = "id")]
    public required string Id { get; set; }
    
    [YamlMember(Alias = "name")]
    public required string Name { get; set; }
    
    [YamlMember(Alias = "description")]
    public string? Description { get; set; }
    
    [YamlMember(Alias = "prompt")]
    public string? Prompt { get; set; }
    
    [YamlMember(Alias = "promptRef")]
    public PromptRefSchema? PromptRef { get; set; }
    
    [YamlMember(Alias = "model")]
    public ModelSchema? Model { get; set; }
    
    [YamlMember(Alias = "capabilities")]
    public List<string> Capabilities { get; set; } = new();
    
    [YamlMember(Alias = "allowedTools")]
    public List<string> AllowedTools { get; set; } = new();
    
    [YamlMember(Alias = "budget")]
    public BudgetSchema? Budget { get; set; }
    
    [YamlMember(Alias = "policy")]
    public PolicySchema? Policy { get; set; }
    
    [YamlMember(Alias = "isOrchestrator")]
    public bool IsOrchestrator { get; set; }
    
    [YamlMember(Alias = "metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public sealed class PromptRefSchema
{
    [YamlMember(Alias = "source")]
    public required string Source { get; set; } // "file", "url", "inline"
    
    [YamlMember(Alias = "value")]
    public required string Value { get; set; }
}

public sealed class ModelSchema
{
    [YamlMember(Alias = "provider")]
    public required string Provider { get; set; }
    
    [YamlMember(Alias = "modelId")]
    public required string ModelId { get; set; }
    
    [YamlMember(Alias = "temperature")]
    public double Temperature { get; set; } = 0.7;
    
    [YamlMember(Alias = "maxTokens")]
    public int? MaxTokens { get; set; }
    
    [YamlMember(Alias = "parameters")]
    public Dictionary<string, object> Parameters { get; set; } = new();
}

public sealed class BudgetSchema
{
    [YamlMember(Alias = "maxTokens")]
    public int? MaxTokens { get; set; }
    
    [YamlMember(Alias = "maxToolCalls")]
    public int? MaxToolCalls { get; set; }
    
    [YamlMember(Alias = "maxDepth")]
    public int? MaxDepth { get; set; }
    
    [YamlMember(Alias = "maxDurationSeconds")]
    public int? MaxDurationSeconds { get; set; }
    
    [YamlMember(Alias = "maxCost")]
    public decimal? MaxCost { get; set; }
}

public sealed class PolicySchema
{
    [YamlMember(Alias = "deniedTools")]
    public List<string> DeniedTools { get; set; } = new();
    
    [YamlMember(Alias = "requiredCapabilities")]
    public List<string> RequiredCapabilities { get; set; } = new();
    
    [YamlMember(Alias = "auditAllToolCalls")]
    public bool AuditAllToolCalls { get; set; } = true;
    
    [YamlMember(Alias = "allowRecursion")]
    public bool AllowRecursion { get; set; } = true;
}

