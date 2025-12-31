using Delgato.Configuration.Builders;

namespace Delgato.Tests.Configuration;

/// <summary>
/// TinyBDD-style tests for AgentDefinitionBuilder.
/// 
/// Scenario: Build agent definition with fluent API
/// </summary>
public class AgentDefinitionBuilderTests
{
    [Fact]
    public void Given_Valid_Configuration_When_Building_Then_Creates_Agent_Definition()
    {
        // Given: A builder with valid configuration
        var builder = new AgentDefinitionBuilder()
            .WithId("test-agent")
            .WithName("Test Agent")
            .WithDescription("A test agent")
            .WithPrompt("You are a test agent")
            .WithCapabilities("testing", "validation");

        // When: Building the definition
        var definition = builder.Build();

        // Then: Creates valid agent definition
        Assert.NotNull(definition);
        Assert.Equal("test-agent", definition.Id.Value);
        Assert.Equal("Test Agent", definition.Name);
        Assert.Equal("A test agent", definition.Description);
        Assert.Contains("testing", definition.Capabilities);
        Assert.Contains("validation", definition.Capabilities);
    }

    [Fact]
    public void Given_PromptRef_From_File_When_Building_Then_Sets_PromptRef_Correctly()
    {
        // Given: A builder with promptRef from file
        var builder = new AgentDefinitionBuilder()
            .WithId("test-agent")
            .WithName("Test Agent")
            .WithPromptFromFile("./prompts/test.txt");

        // When: Building the definition
        var definition = builder.Build();

        // Then: PromptRef is set correctly
        Assert.NotNull(definition.PromptRef);
        Assert.Equal("file", definition.PromptRef.Source);
        Assert.Equal("./prompts/test.txt", definition.PromptRef.Value);
    }

    [Fact]
    public void Given_Model_Configuration_When_Building_Then_Sets_Model_Correctly()
    {
        // Given: A builder with model configuration
        var builder = new AgentDefinitionBuilder()
            .WithId("test-agent")
            .WithName("Test Agent")
            .WithModel(m => m
                .WithProvider("openai")
                .WithModelId("gpt-4")
                .WithTemperature(0.5)
                .WithMaxTokens(2000));

        // When: Building the definition
        var definition = builder.Build();

        // Then: Model is configured correctly
        Assert.NotNull(definition.Model);
        Assert.Equal("openai", definition.Model.Provider);
        Assert.Equal("gpt-4", definition.Model.ModelId);
        Assert.Equal(0.5, definition.Model.Temperature);
        Assert.Equal(2000, definition.Model.MaxTokens);
    }

    [Fact]
    public void Given_Budget_Configuration_When_Building_Then_Sets_Budget_Correctly()
    {
        // Given: A builder with budget configuration
        var builder = new AgentDefinitionBuilder()
            .WithId("test-agent")
            .WithName("Test Agent")
            .WithBudget(b => b
                .WithMaxTokens(10000)
                .WithMaxToolCalls(50)
                .WithMaxDepth(3)
                .WithMaxDuration(TimeSpan.FromMinutes(5))
                .WithMaxCost(1.5m));

        // When: Building the definition
        var definition = builder.Build();

        // Then: Budget is configured correctly
        Assert.NotNull(definition.Budget);
        Assert.Equal(10000, definition.Budget.MaxTokens);
        Assert.Equal(50, definition.Budget.MaxToolCalls);
        Assert.Equal(3, definition.Budget.MaxDepth);
        Assert.Equal(TimeSpan.FromMinutes(5), definition.Budget.MaxDuration);
        Assert.Equal(1.5m, definition.Budget.MaxCost);
    }

    [Fact]
    public void Given_Missing_Required_Fields_When_Building_Then_Throws_Exception()
    {
        // Given: A builder missing required fields
        var builder = new AgentDefinitionBuilder()
            .WithId("test-agent");
        // Missing name

        // When/Then: Building throws exception
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Given_Policy_Configuration_When_Building_Then_Sets_Policy_Correctly()
    {
        // Given: A builder with policy configuration
        var builder = new AgentDefinitionBuilder()
            .WithId("test-agent")
            .WithName("Test Agent")
            .WithPolicy(p => p
                .DenyTool("dangerous_tool")
                .RequireCapability("safe-execution")
                .WithAuditAllToolCalls(true)
                .WithAllowRecursion(false));

        // When: Building the definition
        var definition = builder.Build();

        // Then: Policy is configured correctly
        Assert.NotNull(definition.Policy);
        Assert.Contains("dangerous_tool", definition.Policy.DeniedTools);
        Assert.Contains("safe-execution", definition.Policy.RequiredCapabilities);
        Assert.True(definition.Policy.AuditAllToolCalls);
        Assert.False(definition.Policy.AllowRecursion);
    }

    [Fact]
    public void Given_Orchestrator_Flag_When_Building_Then_Sets_IsOrchestrator()
    {
        // Given: A builder configured as orchestrator
        var builder = new AgentDefinitionBuilder()
            .WithId("orchestrator")
            .WithName("Orchestrator")
            .AsOrchestrator();

        // When: Building the definition
        var definition = builder.Build();

        // Then: IsOrchestrator is true
        Assert.True(definition.IsOrchestrator);
    }

    [Fact]
    public void Given_Multiple_Capabilities_When_Adding_Then_All_Are_Included()
    {
        // Given: A builder with multiple capabilities added separately and in batch
        var builder = new AgentDefinitionBuilder()
            .WithId("test-agent")
            .WithName("Test Agent")
            .WithCapability("capability1")
            .WithCapabilities("capability2", "capability3")
            .WithCapability("capability4");

        // When: Building the definition
        var definition = builder.Build();

        // Then: All capabilities are included
        Assert.Equal(4, definition.Capabilities.Count);
        Assert.Contains("capability1", definition.Capabilities);
        Assert.Contains("capability2", definition.Capabilities);
        Assert.Contains("capability3", definition.Capabilities);
        Assert.Contains("capability4", definition.Capabilities);
    }
}

