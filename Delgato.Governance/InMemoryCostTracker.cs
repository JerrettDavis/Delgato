using System.Collections.Concurrent;
using Delgato.Core;
using Delgato.Core.Governance;
using Microsoft.Extensions.Logging;

namespace Delgato.Governance;

/// <summary>
/// In-memory implementation of cost tracking.
/// Suitable for development and testing. For production, use a persistent store.
/// </summary>
public sealed class InMemoryCostTracker : ICostTracker
{
    private readonly ConcurrentDictionary<Guid, CostEvent> _events = new();
    private readonly ConcurrentDictionary<string, BudgetLimit> _budgetLimits = new();
    private readonly ConcurrentBag<BudgetAlert> _alerts = new();
    private readonly ILogger<InMemoryCostTracker> _logger;

    public InMemoryCostTracker(ILogger<InMemoryCostTracker> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task RecordCostAsync(CostEvent costEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(costEvent);

        _events[costEvent.EventId] = costEvent;

        _logger.LogDebug(
            "Recorded cost event {EventId}: {Provider}/{Model} - {Cost} {Currency} ({Tokens} tokens)",
            costEvent.EventId, costEvent.ProviderId, costEvent.ModelId,
            costEvent.Cost, costEvent.CostCurrency,
            costEvent.InputTokens + costEvent.OutputTokens);

        // Check budget alerts
        _ = CheckAndRaiseBudgetAlerts(costEvent);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<decimal> GetCurrentCostAsync(BudgetScope scope, CancellationToken ct = default)
    {
        var cost = GetFilteredEvents(scope).Sum(e => e.Cost);
        return Task.FromResult(cost);
    }

    /// <inheritdoc/>
    public Task<CostBreakdown> GetBreakdownAsync(CostQuery query, CancellationToken ct = default)
    {
        var events = GetFilteredEvents(query.Scope)
            .Where(e => e.Timestamp >= query.From && e.Timestamp <= query.To)
            .ToList();

        var breakdown = new CostBreakdown
        {
            Scope = query.Scope,
            From = query.From,
            To = query.To,
            TotalCost = events.Sum(e => e.Cost),
            Currency = events.FirstOrDefault()?.CostCurrency ?? "USD",
            TotalTokens = events.Sum(e => e.InputTokens + e.OutputTokens),
            TotalRequests = events.Count,
            Items = BuildBreakdownItems(events, query.GroupBy),
            ByProvider = events.GroupBy(e => e.ProviderId)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Cost)),
            ByModel = events.GroupBy(e => e.ModelId)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Cost)),
            ByAgent = events.GroupBy(e => e.AgentId.Value)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Cost))
        };

        return Task.FromResult(breakdown);
    }

    /// <inheritdoc/>
    public async Task<BudgetCheckResult> CheckBudgetAsync(BudgetScope scope, CancellationToken ct = default)
    {
        var currentCost = await GetCurrentCostAsync(scope, ct);
        var scopeKey = GetScopeKey(scope);

        _budgetLimits.TryGetValue(scopeKey, out var limit);

        var result = new BudgetCheckResult
        {
            IsWithinBudget = limit == null || currentCost < limit.Limit,
            CurrentSpend = currentCost,
            BudgetLimit = limit?.Limit,
            RemainingBudget = limit != null ? limit.Limit - currentCost : null,
            UsagePercentage = limit != null ? (double)(currentCost / limit.Limit * 100) : null,
            Status = DetermineBudgetStatus(currentCost, limit),
            Warnings = GetBudgetWarnings(currentCost, limit)
        };

        return result;
    }

    /// <inheritdoc/>
    public Task SetBudgetLimitAsync(BudgetScope scope, BudgetLimit limit, CancellationToken ct = default)
    {
        var scopeKey = GetScopeKey(scope);
        _budgetLimits[scopeKey] = limit;

        _logger.LogInformation(
            "Set budget limit for scope {ScopeKey}: {Limit} {Currency} per {Period}",
            scopeKey, limit.Limit, limit.Currency, limit.Period);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<BudgetAlert>> GetAlertsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<BudgetAlert>>(_alerts.ToList().AsReadOnly());
    }

    /// <inheritdoc/>
    public async Task<CostForecast> ForecastAsync(BudgetScope scope, TimeSpan period, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var historicalPeriod = TimeSpan.FromDays(30);
        var historicalStart = now - historicalPeriod;

        var events = GetFilteredEvents(scope)
            .Where(e => e.Timestamp >= historicalStart)
            .OrderBy(e => e.Timestamp)
            .ToList();

        if (events.Count == 0)
        {
            return new CostForecast
            {
                Scope = scope,
                ForecastDate = now,
                ForecastPeriod = period,
                PredictedCost = 0,
                LowerBound = 0,
                UpperBound = 0,
                ConfidenceLevel = 0,
                Methodology = "No historical data available"
            };
        }

        // Simple linear projection
        var dailyCost = events.Sum(e => e.Cost) / historicalPeriod.TotalDays;
        var predictedCost = dailyCost * period.TotalDays;

        // Calculate variance for bounds
        var dailyCosts = events
            .GroupBy(e => e.Timestamp.Date)
            .Select(g => g.Sum(e => e.Cost))
            .ToList();

        var variance = dailyCosts.Count > 1
            ? dailyCosts.Select(c => Math.Pow((double)(c - dailyCost), 2)).Average()
            : 0;

        var stdDev = (decimal)Math.Sqrt(variance);
        var marginOfError = stdDev * 1.96m * (decimal)Math.Sqrt(period.TotalDays);

        return new CostForecast
        {
            Scope = scope,
            ForecastDate = now,
            ForecastPeriod = period,
            PredictedCost = predictedCost,
            LowerBound = Math.Max(0, predictedCost - marginOfError),
            UpperBound = predictedCost + marginOfError,
            ConfidenceLevel = 0.95,
            Methodology = "Linear projection with 95% confidence interval",
            TrendData = dailyCosts.Select((c, i) => new CostTrendPoint
            {
                Date = historicalStart.AddDays(i),
                ActualCost = c
            }).ToList()
        };
    }

    private IEnumerable<CostEvent> GetFilteredEvents(BudgetScope scope)
    {
        var events = _events.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(scope.TenantId))
            events = events.Where(e => e.TenantId == scope.TenantId);

        if (!string.IsNullOrEmpty(scope.UserId))
            events = events.Where(e => e.UserId == scope.UserId);

        if (!string.IsNullOrEmpty(scope.ProjectId))
            events = events.Where(e => e.ProjectId == scope.ProjectId);

        if (!string.IsNullOrEmpty(scope.AgentId))
            events = events.Where(e => e.AgentId.Value == scope.AgentId);

        if (!string.IsNullOrEmpty(scope.ProviderId))
            events = events.Where(e => e.ProviderId == scope.ProviderId);

        if (scope.PeriodStart.HasValue)
            events = events.Where(e => e.Timestamp >= scope.PeriodStart.Value);

        if (scope.PeriodEnd.HasValue)
            events = events.Where(e => e.Timestamp <= scope.PeriodEnd.Value);

        return events;
    }

    private IReadOnlyList<CostBreakdownItem> BuildBreakdownItems(List<CostEvent> events, CostGrouping groupBy)
    {
        var totalCost = events.Sum(e => e.Cost);

        IEnumerable<IGrouping<string, CostEvent>> groups = groupBy switch
        {
            CostGrouping.Provider => events.GroupBy(e => e.ProviderId),
            CostGrouping.Model => events.GroupBy(e => e.ModelId),
            CostGrouping.Agent => events.GroupBy(e => e.AgentId.Value),
            CostGrouping.User => events.GroupBy(e => e.UserId ?? "unknown"),
            CostGrouping.Hour => events.GroupBy(e => e.Timestamp.ToString("yyyy-MM-dd HH:00")),
            CostGrouping.Day => events.GroupBy(e => e.Timestamp.ToString("yyyy-MM-dd")),
            CostGrouping.Week => events.GroupBy(e => $"{e.Timestamp.Year}-W{GetIsoWeekNumber(e.Timestamp)}"),
            CostGrouping.Month => events.GroupBy(e => e.Timestamp.ToString("yyyy-MM")),
            _ => events.GroupBy(e => e.Timestamp.ToString("yyyy-MM-dd"))
        };

        return groups.Select(g => new CostBreakdownItem
        {
            Key = g.Key,
            Cost = g.Sum(e => e.Cost),
            Tokens = g.Sum(e => e.InputTokens + e.OutputTokens),
            Requests = g.Count(),
            PercentageOfTotal = totalCost > 0 ? (double)(g.Sum(e => e.Cost) / totalCost * 100) : 0
        }).ToList();
    }

    private static int GetIsoWeekNumber(DateTimeOffset date)
    {
        var day = (int)date.DayOfWeek;
        day = day == 0 ? 7 : day;
        var thursday = date.AddDays(4 - day);
        var jan1 = new DateTimeOffset(thursday.Year, 1, 1, 0, 0, 0, thursday.Offset);
        return (thursday - jan1).Days / 7 + 1;
    }

    private string GetScopeKey(BudgetScope scope)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(scope.TenantId))
            parts.Add($"tenant:{scope.TenantId}");
        if (!string.IsNullOrEmpty(scope.UserId))
            parts.Add($"user:{scope.UserId}");
        if (!string.IsNullOrEmpty(scope.ProjectId))
            parts.Add($"project:{scope.ProjectId}");
        if (!string.IsNullOrEmpty(scope.AgentId))
            parts.Add($"agent:{scope.AgentId}");
        if (!string.IsNullOrEmpty(scope.ProviderId))
            parts.Add($"provider:{scope.ProviderId}");

        return parts.Count > 0 ? string.Join("|", parts) : "global";
    }

    private BudgetStatus DetermineBudgetStatus(decimal currentCost, BudgetLimit? limit)
    {
        if (limit == null) return BudgetStatus.Healthy;

        var usagePercent = currentCost / limit.Limit * 100;

        if (currentCost >= limit.Limit) return BudgetStatus.Exceeded;
        if (limit.CriticalThreshold.HasValue && usagePercent >= (double)limit.CriticalThreshold.Value)
            return BudgetStatus.Critical;
        if (limit.WarningThreshold.HasValue && usagePercent >= (double)limit.WarningThreshold.Value)
            return BudgetStatus.Warning;

        return BudgetStatus.Healthy;
    }

    private IReadOnlyList<string> GetBudgetWarnings(decimal currentCost, BudgetLimit? limit)
    {
        var warnings = new List<string>();
        if (limit == null) return warnings;

        var usagePercent = currentCost / limit.Limit * 100;

        if (currentCost >= limit.Limit)
            warnings.Add($"Budget exceeded: {currentCost:C} of {limit.Limit:C}");
        else if (usagePercent >= 90)
            warnings.Add($"Budget usage at {usagePercent:F1}%");
        else if (usagePercent >= 75)
            warnings.Add($"Budget usage approaching limit: {usagePercent:F1}%");

        return warnings;
    }

    private async Task CheckAndRaiseBudgetAlerts(CostEvent costEvent)
    {
        var scopes = new[]
        {
            new BudgetScope { Type = BudgetScopeType.Global },
            new BudgetScope { Type = BudgetScopeType.Provider, ProviderId = costEvent.ProviderId },
            new BudgetScope { Type = BudgetScopeType.Agent, AgentId = costEvent.AgentId.Value }
        };

        foreach (var scope in scopes)
        {
            var result = await CheckBudgetAsync(scope);
            if (result.Status != BudgetStatus.Healthy)
            {
                var alert = new BudgetAlert
                {
                    AlertId = Guid.NewGuid(),
                    Timestamp = DateTimeOffset.UtcNow,
                    Scope = scope,
                    Type = result.Status switch
                    {
                        BudgetStatus.Exceeded => BudgetAlertType.Exceeded,
                        BudgetStatus.Critical => BudgetAlertType.Critical,
                        _ => BudgetAlertType.Warning
                    },
                    Message = result.Warnings.FirstOrDefault() ?? $"Budget {result.Status}",
                    CurrentSpend = result.CurrentSpend,
                    Threshold = result.BudgetLimit ?? 0
                };

                _alerts.Add(alert);
                _logger.LogWarning("Budget alert: {Message}", alert.Message);
            }
        }
    }

    /// <summary>
    /// Clears all data. For testing purposes only.
    /// </summary>
    public void Clear()
    {
        _events.Clear();
        _budgetLimits.Clear();
        _alerts.Clear();
    }
}
