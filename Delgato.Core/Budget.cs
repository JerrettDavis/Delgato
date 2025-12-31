namespace Delgato.Core;

/// <summary>
/// Budget constraints for agent execution.
/// </summary>
public sealed record Budget
{
    public int? MaxTokens { get; init; }
    public int? MaxToolCalls { get; init; }
    public int? MaxDepth { get; init; }
    public TimeSpan? MaxDuration { get; init; }
    public decimal? MaxCost { get; init; }

    public static Budget Default => new()
    {
        MaxTokens = 100_000,
        MaxToolCalls = 50,
        MaxDepth = 5,
        MaxDuration = TimeSpan.FromMinutes(5),
        MaxCost = 1.0m
    };

    public static Budget Unlimited => new();
}

