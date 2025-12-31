using Delgato.Core;

namespace Delgato.Configuration.Builders;

/// <summary>
/// Fluent builder for creating AgentDefinition instances.
/// </summary>
public sealed class AgentDefinitionBuilder
{
    private AgentId? _id;
    private string? _name;
    private string? _description;
    private string? _prompt;
    private PromptReference? _promptRef;
    private ModelConfiguration? _model;
    private readonly List<string> _capabilities = new();
    private readonly List<string> _allowedTools = new();
    private Budget? _budget;
    private PolicyConfiguration? _policy;
    private bool _isOrchestrator;
    private readonly Dictionary<string, object> _metadata = new();

    public AgentDefinitionBuilder WithId(string id)
    {
        _id = new AgentId(id);
        return this;
    }

    public AgentDefinitionBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public AgentDefinitionBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public AgentDefinitionBuilder WithPrompt(string prompt)
    {
        _prompt = prompt;
        return this;
    }

    public AgentDefinitionBuilder WithPromptRef(string source, string value)
    {
        _promptRef = new PromptReference { Source = source, Value = value };
        return this;
    }

    public AgentDefinitionBuilder WithPromptFromFile(string filePath)
    {
        return WithPromptRef("file", filePath);
    }

    public AgentDefinitionBuilder WithPromptFromUrl(string url)
    {
        return WithPromptRef("url", url);
    }

    public AgentDefinitionBuilder WithModel(Action<ModelConfigurationBuilder> configure)
    {
        var builder = new ModelConfigurationBuilder();
        configure(builder);
        _model = builder.Build();
        return this;
    }

    public AgentDefinitionBuilder WithCapability(string capability)
    {
        _capabilities.Add(capability);
        return this;
    }

    public AgentDefinitionBuilder WithCapabilities(params string[] capabilities)
    {
        _capabilities.AddRange(capabilities);
        return this;
    }

    public AgentDefinitionBuilder WithAllowedTool(string toolId)
    {
        _allowedTools.Add(toolId);
        return this;
    }

    public AgentDefinitionBuilder WithAllowedTools(params string[] toolIds)
    {
        _allowedTools.AddRange(toolIds);
        return this;
    }

    public AgentDefinitionBuilder WithBudget(Action<BudgetBuilder> configure)
    {
        var builder = new BudgetBuilder();
        configure(builder);
        _budget = builder.Build();
        return this;
    }

    public AgentDefinitionBuilder WithPolicy(Action<PolicyConfigurationBuilder> configure)
    {
        var builder = new PolicyConfigurationBuilder();
        configure(builder);
        _policy = builder.Build();
        return this;
    }

    public AgentDefinitionBuilder AsOrchestrator()
    {
        _isOrchestrator = true;
        return this;
    }

    public AgentDefinitionBuilder WithMetadata(string key, object value)
    {
        _metadata[key] = value;
        return this;
    }

    public AgentDefinition Build()
    {
        if (_id is null || string.IsNullOrWhiteSpace(_id.Value.Value))
            throw new InvalidOperationException("Agent ID is required.");
        
        if (string.IsNullOrWhiteSpace(_name))
            throw new InvalidOperationException("Agent name is required.");

        return new AgentDefinition
        {
            Id = _id.Value,
            Name = _name,
            Description = _description,
            Prompt = _prompt,
            PromptRef = _promptRef,
            Model = _model,
            Capabilities = _capabilities,
            AllowedTools = _allowedTools,
            Budget = _budget,
            Policy = _policy,
            IsOrchestrator = _isOrchestrator,
            Metadata = _metadata
        };
    }
}

public sealed class ModelConfigurationBuilder
{
    private string? _provider;
    private string? _modelId;
    private double _temperature = 0.7;
    private int? _maxTokens;
    private readonly Dictionary<string, object> _parameters = new();

    public ModelConfigurationBuilder WithProvider(string provider)
    {
        _provider = provider;
        return this;
    }

    public ModelConfigurationBuilder WithModelId(string modelId)
    {
        _modelId = modelId;
        return this;
    }

    public ModelConfigurationBuilder WithTemperature(double temperature)
    {
        _temperature = temperature;
        return this;
    }

    public ModelConfigurationBuilder WithMaxTokens(int maxTokens)
    {
        _maxTokens = maxTokens;
        return this;
    }

    public ModelConfigurationBuilder WithParameter(string key, object value)
    {
        _parameters[key] = value;
        return this;
    }

    public ModelConfiguration Build()
    {
        if (string.IsNullOrWhiteSpace(_provider))
            throw new InvalidOperationException("Provider is required.");
        
        if (string.IsNullOrWhiteSpace(_modelId))
            throw new InvalidOperationException("ModelId is required.");

        return new ModelConfiguration
        {
            Provider = _provider,
            ModelId = _modelId,
            Temperature = _temperature,
            MaxTokens = _maxTokens,
            Parameters = _parameters
        };
    }
}

public sealed class BudgetBuilder
{
    private int? _maxTokens;
    private int? _maxToolCalls;
    private int? _maxDepth;
    private TimeSpan? _maxDuration;
    private decimal? _maxCost;

    public BudgetBuilder WithMaxTokens(int maxTokens)
    {
        _maxTokens = maxTokens;
        return this;
    }

    public BudgetBuilder WithMaxToolCalls(int maxToolCalls)
    {
        _maxToolCalls = maxToolCalls;
        return this;
    }

    public BudgetBuilder WithMaxDepth(int maxDepth)
    {
        _maxDepth = maxDepth;
        return this;
    }

    public BudgetBuilder WithMaxDuration(TimeSpan maxDuration)
    {
        _maxDuration = maxDuration;
        return this;
    }

    public BudgetBuilder WithMaxCost(decimal maxCost)
    {
        _maxCost = maxCost;
        return this;
    }

    public Budget Build()
    {
        return new Budget
        {
            MaxTokens = _maxTokens,
            MaxToolCalls = _maxToolCalls,
            MaxDepth = _maxDepth,
            MaxDuration = _maxDuration,
            MaxCost = _maxCost
        };
    }
}

public sealed class PolicyConfigurationBuilder
{
    private readonly List<string> _deniedTools = new();
    private readonly List<string> _requiredCapabilities = new();
    private bool _auditAllToolCalls = true;
    private bool _allowRecursion = true;

    public PolicyConfigurationBuilder DenyTool(string toolId)
    {
        _deniedTools.Add(toolId);
        return this;
    }

    public PolicyConfigurationBuilder RequireCapability(string capability)
    {
        _requiredCapabilities.Add(capability);
        return this;
    }

    public PolicyConfigurationBuilder WithAuditAllToolCalls(bool audit = true)
    {
        _auditAllToolCalls = audit;
        return this;
    }

    public PolicyConfigurationBuilder WithAllowRecursion(bool allow = true)
    {
        _allowRecursion = allow;
        return this;
    }

    public PolicyConfiguration Build()
    {
        return new PolicyConfiguration
        {
            DeniedTools = _deniedTools,
            RequiredCapabilities = _requiredCapabilities,
            AuditAllToolCalls = _auditAllToolCalls,
            AllowRecursion = _allowRecursion
        };
    }
}

