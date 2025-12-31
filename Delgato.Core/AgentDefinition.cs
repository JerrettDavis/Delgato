namespace Delgato.Core;

/// <summary>
/// Definition of an agent loaded from DSL (YAML/JSON).
/// </summary>
public sealed record AgentDefinition
{
    public required AgentId Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Prompt { get; init; }
    public PromptReference? PromptRef { get; init; }
    public ModelConfiguration? Model { get; init; }
    public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AllowedTools { get; init; } = Array.Empty<string>();
    public Budget? Budget { get; init; }
    public PolicyConfiguration? Policy { get; init; }
    public bool IsOrchestrator { get; init; }
    public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}

public sealed record PromptReference
{
    public required string Source { get; init; } // "file", "url", "inline"
    public required string Value { get; init; }
}

public sealed record ModelConfiguration
{
    public required string Provider { get; init; } // "openai", "azure", "anthropic", etc.
    public required string ModelId { get; init; }
    public double Temperature { get; init; } = 0.7;
    public int? MaxTokens { get; init; }
    public IDictionary<string, object> Parameters { get; init; } = new Dictionary<string, object>();
}

public sealed record PolicyConfiguration
{
    public IReadOnlyList<string> DeniedTools { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RequiredCapabilities { get; init; } = Array.Empty<string>();
    public bool AuditAllToolCalls { get; init; } = true;
    public bool AllowRecursion { get; init; } = true;
}

