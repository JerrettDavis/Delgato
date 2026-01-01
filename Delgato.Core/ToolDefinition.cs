namespace Delgato.Core;

/// <summary>
/// Definition of a tool that can be invoked by agents.
/// </summary>
public sealed record ToolDefinition
{
    public required string ToolId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required ToolType Type { get; init; }
    public IDictionary<string, ParameterDefinition> Parameters { get; init; } = new Dictionary<string, ParameterDefinition>();
    public IDictionary<string, object> Configuration { get; init; } = new Dictionary<string, object>();
    public bool RequiresApproval { get; init; }

    /// <summary>
    /// JSON Schema for the tool's input parameters (used by LLM providers).
    /// </summary>
    public object? InputSchema { get; init; }
}

public enum ToolType
{
    SemanticKernelFunction,
    HttpTool,
    FileSystemTool,
    McpTool,
    DatabaseTool,
    Custom
}

public sealed record ParameterDefinition
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public string? Description { get; init; }
    public bool Required { get; init; }
    public object? DefaultValue { get; init; }
}

