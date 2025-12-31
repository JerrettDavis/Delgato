using Delgato.Core;
using ExecutionContext = Delgato.Core.ExecutionContext;

namespace Delgato.Tests.Core;

/// <summary>
/// TinyBDD-style tests for ExecutionContext budget enforcement.
/// 
/// Scenario: Budget enforcement prevents excessive resource consumption
/// </summary>
public class ExecutionContextTests
{
    [Fact]
    public void Given_Context_Within_Budget_When_Checking_Then_Returns_True()
    {
        // Given: A context with budget and low consumption
        var context = new ExecutionContext
        {
            CorrelationId = "test-1",
            Budget = new Budget
            {
                MaxTokens = 1000,
                MaxToolCalls = 10,
                MaxDepth = 3,
                MaxDuration = TimeSpan.FromMinutes(5),
                MaxCost = 1.0m
            },
            TokensConsumed = 100,
            ToolCallsExecuted = 2,
            CurrentDepth = 1,
            CostAccumulated = 0.1m
        };

        // When: Checking if within budget
        var isWithinBudget = context.IsWithinBudget();

        // Then: Returns true
        Assert.True(isWithinBudget);
    }

    [Fact]
    public void Given_Token_Limit_Exceeded_When_Checking_Then_Returns_False()
    {
        // Given: A context that exceeded token limit
        var context = new ExecutionContext
        {
            CorrelationId = "test-2",
            Budget = new Budget { MaxTokens = 1000 },
            TokensConsumed = 1001
        };

        // When: Checking if within budget
        var isWithinBudget = context.IsWithinBudget();

        // Then: Returns false
        Assert.False(isWithinBudget);
    }

    [Fact]
    public void Given_Tool_Call_Limit_Exceeded_When_Checking_Then_Returns_False()
    {
        // Given: A context that exceeded tool call limit
        var context = new ExecutionContext
        {
            CorrelationId = "test-3",
            Budget = new Budget { MaxToolCalls = 10 },
            ToolCallsExecuted = 11
        };

        // When: Checking if within budget
        var isWithinBudget = context.IsWithinBudget();

        // Then: Returns false
        Assert.False(isWithinBudget);
    }

    [Fact]
    public void Given_Depth_Limit_Exceeded_When_Checking_Then_Returns_False()
    {
        // Given: A context that exceeded depth limit
        var context = new ExecutionContext
        {
            CorrelationId = "test-4",
            Budget = new Budget { MaxDepth = 3 },
            CurrentDepth = 4
        };

        // When: Checking if within budget
        var isWithinBudget = context.IsWithinBudget();

        // Then: Returns false
        Assert.False(isWithinBudget);
    }

    [Fact]
    public async Task Given_Duration_Limit_Exceeded_When_Checking_Then_Returns_False()
    {
        // Given: A context that exceeded duration limit
        var context = new ExecutionContext
        {
            CorrelationId = "test-5",
            Budget = new Budget { MaxDuration = TimeSpan.FromMilliseconds(100) },
            StartedAt = DateTimeOffset.UtcNow.AddSeconds(-1)
        };

        // Wait to exceed duration
        await Task.Delay(200);

        // When: Checking if within budget
        var isWithinBudget = context.IsWithinBudget();

        // Then: Returns false
        Assert.False(isWithinBudget);
    }

    [Fact]
    public void Given_Cost_Limit_Exceeded_When_Checking_Then_Returns_False()
    {
        // Given: A context that exceeded cost limit
        var context = new ExecutionContext
        {
            CorrelationId = "test-6",
            Budget = new Budget { MaxCost = 1.0m },
            CostAccumulated = 1.5m
        };

        // When: Checking if within budget
        var isWithinBudget = context.IsWithinBudget();

        // Then: Returns false
        Assert.False(isWithinBudget);
    }

    [Fact]
    public void Given_Unlimited_Budget_When_Checking_Then_Always_Returns_True()
    {
        // Given: A context with unlimited budget
        var context = new ExecutionContext
        {
            CorrelationId = "test-7",
            Budget = Budget.Unlimited,
            TokensConsumed = 1_000_000,
            ToolCallsExecuted = 1000,
            CurrentDepth = 100,
            CostAccumulated = 999.99m
        };

        // When: Checking if within budget
        var isWithinBudget = context.IsWithinBudget();

        // Then: Returns true (no limits set)
        Assert.True(isWithinBudget);
    }

    [Fact]
    public void Given_Default_Budget_When_Accessing_Then_Has_Reasonable_Limits()
    {
        // Given/When: Accessing default budget
        var budget = Budget.Default;

        // Then: Has reasonable default limits
        Assert.NotNull(budget.MaxTokens);
        Assert.True(budget.MaxTokens > 0);
        Assert.NotNull(budget.MaxToolCalls);
        Assert.True(budget.MaxToolCalls > 0);
        Assert.NotNull(budget.MaxDepth);
        Assert.True(budget.MaxDepth > 0);
        Assert.NotNull(budget.MaxDuration);
        Assert.True(budget.MaxDuration > TimeSpan.Zero);
    }
}

