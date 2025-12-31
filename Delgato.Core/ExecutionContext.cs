namespace Delgato.Core;

/// <summary>
/// Execution context tracking resource consumption and limits.
/// </summary>
public sealed class ExecutionContext
{
    public required string CorrelationId { get; init; }
    public required Budget Budget { get; init; }
    public int CurrentDepth { get; set; }
    public int TokensConsumed { get; set; }
    public int ToolCallsExecuted { get; set; }
    public decimal CostAccumulated { get; set; }
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public CancellationToken CancellationToken { get; init; } = CancellationToken.None;

    public IDictionary<string, object> State { get; } = new Dictionary<string, object>();

    public bool IsWithinBudget()
    {
        if (Budget.MaxTokens.HasValue && TokensConsumed >= Budget.MaxTokens.Value)
            return false;
        
        if (Budget.MaxToolCalls.HasValue && ToolCallsExecuted >= Budget.MaxToolCalls.Value)
            return false;
        
        if (Budget.MaxDepth.HasValue && CurrentDepth >= Budget.MaxDepth.Value)
            return false;
        
        if (Budget.MaxDuration.HasValue && (DateTimeOffset.UtcNow - StartedAt) >= Budget.MaxDuration.Value)
            return false;
        
        if (Budget.MaxCost.HasValue && CostAccumulated >= Budget.MaxCost.Value)
            return false;

        return true;
    }
}

