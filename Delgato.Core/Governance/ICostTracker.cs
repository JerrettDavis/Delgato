namespace Delgato.Core.Governance;

/// <summary>
/// Tracks and manages costs across the agent swarm.
/// Enterprise cost management and budget enforcement.
/// </summary>
public interface ICostTracker
{
    /// <summary>
    /// Records a cost event.
    /// </summary>
    Task RecordCostAsync(CostEvent costEvent, CancellationToken ct = default);

    /// <summary>
    /// Gets current cost for a budget scope.
    /// </summary>
    Task<decimal> GetCurrentCostAsync(BudgetScope scope, CancellationToken ct = default);

    /// <summary>
    /// Gets cost breakdown for a time range.
    /// </summary>
    Task<CostBreakdown> GetBreakdownAsync(CostQuery query, CancellationToken ct = default);

    /// <summary>
    /// Checks if a budget scope has exceeded its limit.
    /// </summary>
    Task<BudgetCheckResult> CheckBudgetAsync(BudgetScope scope, CancellationToken ct = default);

    /// <summary>
    /// Sets a budget limit for a scope.
    /// </summary>
    Task SetBudgetLimitAsync(BudgetScope scope, BudgetLimit limit, CancellationToken ct = default);

    /// <summary>
    /// Gets all budget alerts.
    /// </summary>
    Task<IReadOnlyList<BudgetAlert>> GetAlertsAsync(CancellationToken ct = default);

    /// <summary>
    /// Forecasts costs for a future period.
    /// </summary>
    Task<CostForecast> ForecastAsync(BudgetScope scope, TimeSpan period, CancellationToken ct = default);
}

/// <summary>
/// A cost event in the system.
/// </summary>
public sealed record CostEvent
{
    public required Guid EventId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string ProviderId { get; init; }
    public required string ModelId { get; init; }
    public required AgentId AgentId { get; init; }
    public required string CorrelationId { get; init; }
    public required int InputTokens { get; init; }
    public required int OutputTokens { get; init; }
    public required decimal Cost { get; init; }
    public required string CostCurrency { get; init; }
    public string? UserId { get; init; }
    public string? TenantId { get; init; }
    public string? ProjectId { get; init; }
    public IDictionary<string, object> Tags { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Scope for budget tracking.
/// </summary>
public sealed record BudgetScope
{
    public BudgetScopeType Type { get; init; }
    public string? TenantId { get; init; }
    public string? UserId { get; init; }
    public string? ProjectId { get; init; }
    public string? AgentId { get; init; }
    public string? ProviderId { get; init; }
    public DateTimeOffset? PeriodStart { get; init; }
    public DateTimeOffset? PeriodEnd { get; init; }
}

/// <summary>
/// Type of budget scope.
/// </summary>
public enum BudgetScopeType
{
    Global,
    Tenant,
    User,
    Project,
    Agent,
    Provider
}

/// <summary>
/// Query for cost data.
/// </summary>
public sealed record CostQuery
{
    public required BudgetScope Scope { get; init; }
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }
    public CostGrouping GroupBy { get; init; } = CostGrouping.Day;
}

/// <summary>
/// Grouping for cost breakdown.
/// </summary>
public enum CostGrouping
{
    Hour,
    Day,
    Week,
    Month,
    Provider,
    Model,
    Agent,
    User
}

/// <summary>
/// Breakdown of costs.
/// </summary>
public sealed record CostBreakdown
{
    public required BudgetScope Scope { get; init; }
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }
    public required decimal TotalCost { get; init; }
    public required string Currency { get; init; }
    public required int TotalTokens { get; init; }
    public required int TotalRequests { get; init; }
    public required IReadOnlyList<CostBreakdownItem> Items { get; init; }
    public required IReadOnlyDictionary<string, decimal> ByProvider { get; init; }
    public required IReadOnlyDictionary<string, decimal> ByModel { get; init; }
    public required IReadOnlyDictionary<string, decimal> ByAgent { get; init; }
}

/// <summary>
/// Individual item in cost breakdown.
/// </summary>
public sealed record CostBreakdownItem
{
    public required string Key { get; init; }
    public required decimal Cost { get; init; }
    public required int Tokens { get; init; }
    public required int Requests { get; init; }
    public required double PercentageOfTotal { get; init; }
}

/// <summary>
/// Result of budget check.
/// </summary>
public sealed record BudgetCheckResult
{
    public required bool IsWithinBudget { get; init; }
    public required decimal CurrentSpend { get; init; }
    public required decimal? BudgetLimit { get; init; }
    public required decimal? RemainingBudget { get; init; }
    public required double? UsagePercentage { get; init; }
    public required BudgetStatus Status { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Budget status.
/// </summary>
public enum BudgetStatus
{
    Healthy,
    Warning,
    Critical,
    Exceeded
}

/// <summary>
/// Budget limit configuration.
/// </summary>
public sealed record BudgetLimit
{
    public required decimal Limit { get; init; }
    public required string Currency { get; init; }
    public required BudgetPeriod Period { get; init; }
    public decimal? WarningThreshold { get; init; }
    public decimal? CriticalThreshold { get; init; }
    public bool EnforceHardLimit { get; init; }
    public IReadOnlyList<string> NotificationEmails { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Budget period.
/// </summary>
public enum BudgetPeriod
{
    Daily,
    Weekly,
    Monthly,
    Quarterly,
    Yearly,
    Custom
}

/// <summary>
/// Budget alert.
/// </summary>
public sealed record BudgetAlert
{
    public required Guid AlertId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required BudgetScope Scope { get; init; }
    public required BudgetAlertType Type { get; init; }
    public required string Message { get; init; }
    public required decimal CurrentSpend { get; init; }
    public required decimal Threshold { get; init; }
    public bool IsAcknowledged { get; init; }
    public DateTimeOffset? AcknowledgedAt { get; init; }
    public string? AcknowledgedBy { get; init; }
}

/// <summary>
/// Type of budget alert.
/// </summary>
public enum BudgetAlertType
{
    Warning,
    Critical,
    Exceeded
}

/// <summary>
/// Cost forecast.
/// </summary>
public sealed record CostForecast
{
    public required BudgetScope Scope { get; init; }
    public required DateTimeOffset ForecastDate { get; init; }
    public required TimeSpan ForecastPeriod { get; init; }
    public required decimal PredictedCost { get; init; }
    public required decimal LowerBound { get; init; }
    public required decimal UpperBound { get; init; }
    public required double ConfidenceLevel { get; init; }
    public required string Methodology { get; init; }
    public IReadOnlyList<CostTrendPoint> TrendData { get; init; } = Array.Empty<CostTrendPoint>();
}

/// <summary>
/// Point in cost trend data.
/// </summary>
public sealed record CostTrendPoint
{
    public required DateTimeOffset Date { get; init; }
    public required decimal ActualCost { get; init; }
    public decimal? PredictedCost { get; init; }
}
