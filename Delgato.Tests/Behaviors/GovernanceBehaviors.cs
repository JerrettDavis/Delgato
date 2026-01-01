using Delgato.Core;
using Delgato.Core.Governance;
using Delgato.Governance;
using Microsoft.Extensions.Logging.Abstractions;

namespace Delgato.Tests.Behaviors;

/// <summary>
/// TinyBDD behavior tests for Governance (Audit & Cost Tracking) functionality.
/// </summary>
public class GovernanceBehaviors
{
    private IAuditService? _auditService;
    private ICostTracker? _costTracker;
    private AuditEvent? _auditEvent;
    private CostEvent? _costEvent;
    private AuditQueryResult? _queryResult;
    private BudgetCheckResult? _budgetResult;

    #region Given/When/Then Helpers

    private void Given_An_Audit_Service()
    {
        _auditService = new InMemoryAuditService(NullLogger<InMemoryAuditService>.Instance);
    }

    private void Given_A_Cost_Tracker()
    {
        _costTracker = new InMemoryCostTracker(NullLogger<InMemoryCostTracker>.Instance);
    }

    private void Given_An_Audit_Event()
    {
        _auditEvent = new AuditEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            EventType = "AgentExecution",
            Category = AuditEventCategory.Agent,
            Severity = AuditEventSeverity.Info,
            ActorId = "user-123",
            ActorType = ActorType.User,
            ResourceType = "Agent",
            ResourceId = "agent-456",
            Action = "Execute",
            Outcome = AuditOutcome.Success,
            CorrelationId = Guid.NewGuid().ToString(),
            Duration = TimeSpan.FromMilliseconds(100)
        };
    }

    private void Given_A_Cost_Event()
    {
        _costEvent = new CostEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            ProviderId = "openai",
            ModelId = "gpt-4",
            AgentId = new AgentId("agent-123"),
            CorrelationId = Guid.NewGuid().ToString(),
            InputTokens = 100,
            OutputTokens = 50,
            Cost = 0.01m,
            CostCurrency = "USD"
        };
    }

    private async Task When_Audit_Event_Is_Recorded()
    {
        await _auditService!.RecordAsync(_auditEvent!);
    }

    private async Task When_Cost_Event_Is_Recorded()
    {
        await _costTracker!.RecordCostAsync(_costEvent!);
    }

    private async Task When_Audit_Is_Queried()
    {
        _queryResult = await _auditService!.QueryAsync(new AuditQuery());
    }

    private async Task When_Budget_Is_Checked()
    {
        _budgetResult = await _costTracker!.CheckBudgetAsync(new BudgetScope
        {
            Type = BudgetScopeType.Global
        });
    }

    #endregion

    [Fact]
    public async Task Audit_Service_Should_Record_Events()
    {
        // Given
        Given_An_Audit_Service();
        Given_An_Audit_Event();

        // When
        await When_Audit_Event_Is_Recorded();

        // Then
        var retrieved = await _auditService!.GetByIdAsync(_auditEvent!.EventId);
        Assert.NotNull(retrieved);
        Assert.Equal(_auditEvent.EventType, retrieved.EventType);
    }

    [Fact]
    public async Task Audit_Service_Should_Query_Events()
    {
        // Given
        Given_An_Audit_Service();
        Given_An_Audit_Event();
        await When_Audit_Event_Is_Recorded();

        // When
        await When_Audit_Is_Queried();

        // Then
        Assert.NotNull(_queryResult);
        Assert.Single(_queryResult.Events);
    }

    [Fact]
    public async Task Audit_Service_Should_Filter_By_Event_Type()
    {
        // Given
        Given_An_Audit_Service();
        Given_An_Audit_Event();
        await When_Audit_Event_Is_Recorded();

        // When
        var query = new AuditQuery { EventTypes = new[] { "AgentExecution" } };
        var result = await _auditService!.QueryAsync(query);

        // Then
        Assert.Single(result.Events);
    }

    [Fact]
    public async Task Audit_Service_Should_Get_Events_By_Correlation_Id()
    {
        // Given
        Given_An_Audit_Service();
        Given_An_Audit_Event();
        await When_Audit_Event_Is_Recorded();

        // When
        var events = await _auditService!.GetByCorrelationIdAsync(_auditEvent!.CorrelationId!);

        // Then
        Assert.Single(events);
    }

    [Fact]
    public async Task Audit_Service_Should_Calculate_Statistics()
    {
        // Given
        Given_An_Audit_Service();
        Given_An_Audit_Event();
        await When_Audit_Event_Is_Recorded();

        // When
        var stats = await _auditService!.GetStatisticsAsync(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));

        // Then
        Assert.Equal(1, stats.TotalEvents);
        Assert.Equal(1.0, stats.SuccessRate);
    }

    [Fact]
    public async Task Cost_Tracker_Should_Record_Costs()
    {
        // Given
        Given_A_Cost_Tracker();
        Given_A_Cost_Event();

        // When
        await When_Cost_Event_Is_Recorded();

        // Then
        var cost = await _costTracker!.GetCurrentCostAsync(new BudgetScope
        {
            Type = BudgetScopeType.Global
        });
        Assert.Equal(_costEvent!.Cost, cost);
    }

    [Fact]
    public async Task Cost_Tracker_Should_Check_Budget()
    {
        // Given
        Given_A_Cost_Tracker();
        Given_A_Cost_Event();
        await When_Cost_Event_Is_Recorded();

        // When
        await When_Budget_Is_Checked();

        // Then
        Assert.NotNull(_budgetResult);
        Assert.True(_budgetResult.IsWithinBudget);
    }

    [Fact]
    public async Task Cost_Tracker_Should_Enforce_Budget_Limits()
    {
        // Given
        Given_A_Cost_Tracker();
        await _costTracker!.SetBudgetLimitAsync(
            new BudgetScope { Type = BudgetScopeType.Global },
            new BudgetLimit
            {
                Limit = 0.005m,
                Currency = "USD",
                Period = BudgetPeriod.Daily
            });

        Given_A_Cost_Event();
        await When_Cost_Event_Is_Recorded();

        // When
        await When_Budget_Is_Checked();

        // Then
        Assert.False(_budgetResult!.IsWithinBudget);
        Assert.Equal(BudgetStatus.Exceeded, _budgetResult.Status);
    }

    [Fact]
    public async Task Cost_Tracker_Should_Get_Cost_Breakdown()
    {
        // Given
        Given_A_Cost_Tracker();
        Given_A_Cost_Event();
        await When_Cost_Event_Is_Recorded();

        // When
        var breakdown = await _costTracker!.GetBreakdownAsync(new CostQuery
        {
            Scope = new BudgetScope { Type = BudgetScopeType.Global },
            From = DateTimeOffset.UtcNow.AddDays(-1),
            To = DateTimeOffset.UtcNow.AddDays(1),
            GroupBy = CostGrouping.Provider
        });

        // Then
        Assert.Equal(_costEvent!.Cost, breakdown.TotalCost);
        Assert.Contains("openai", breakdown.ByProvider.Keys);
    }

    [Fact]
    public async Task Cost_Tracker_Should_Generate_Alerts()
    {
        // Given
        Given_A_Cost_Tracker();
        await _costTracker!.SetBudgetLimitAsync(
            new BudgetScope { Type = BudgetScopeType.Global },
            new BudgetLimit
            {
                Limit = 0.001m,
                Currency = "USD",
                Period = BudgetPeriod.Daily,
                WarningThreshold = 80m,
                CriticalThreshold = 90m
            });

        Given_A_Cost_Event();
        await When_Cost_Event_Is_Recorded();

        // When
        var alerts = await _costTracker!.GetAlertsAsync();

        // Then
        Assert.NotEmpty(alerts);
    }

    [Fact]
    public async Task Cost_Tracker_Should_Forecast_Costs()
    {
        // Given
        Given_A_Cost_Tracker();
        Given_A_Cost_Event();
        await When_Cost_Event_Is_Recorded();

        // When
        var forecast = await _costTracker!.ForecastAsync(
            new BudgetScope { Type = BudgetScopeType.Global },
            TimeSpan.FromDays(30));

        // Then
        Assert.NotNull(forecast);
        Assert.True(forecast.PredictedCost >= 0);
    }
}
