using Delgato.Core;
using Delgato.Core.Abstractions;
using Delgato.Orchestration;
using Microsoft.Extensions.Logging.Abstractions;
using ExecutionContext = Delgato.Core.ExecutionContext;

namespace Delgato.Tests.Orchestration;

/// <summary>
/// TinyBDD-style tests for SwarmOrchestrator.
/// 
/// Scenario: Orchestrator routes requests to capable agents
/// </summary>
public class SwarmOrchestratorTests
{
    [Fact]
    public async Task Given_Orchestrator_Agent_Exists_When_Routing_Then_Prefers_Orchestrator()
    {
        // Given: Registry with orchestrator and regular agents
        var registry = CreateInMemoryRegistry();
        await registry.RegisterAsync(CreateAgentDefinition("orchestrator", isOrchestrator: true));
        await registry.RegisterAsync(CreateAgentDefinition("worker", isOrchestrator: false));

        var orchestrator = CreateOrchestrator(registry);
        var request = CreateRequest("test request");

        // When: Routing a request
        var agentId = await orchestrator.RouteRequestAsync(request);

        // Then: Routes to orchestrator
        Assert.NotNull(agentId);
        Assert.Equal("orchestrator", agentId.Value.Value);
    }

    [Fact]
    public async Task Given_No_Orchestrator_When_Routing_Then_Routes_To_First_Agent()
    {
        // Given: Registry with only regular agents
        var registry = CreateInMemoryRegistry();
        await registry.RegisterAsync(CreateAgentDefinition("worker1", isOrchestrator: false));
        await registry.RegisterAsync(CreateAgentDefinition("worker2", isOrchestrator: false));

        var orchestrator = CreateOrchestrator(registry);
        var request = CreateRequest("test request");

        // When: Routing a request
        var agentId = await orchestrator.RouteRequestAsync(request);

        // Then: Routes to first available agent
        Assert.NotNull(agentId);
        Assert.Equal("worker1", agentId.Value.Value);
    }

    [Fact]
    public async Task Given_Empty_Registry_When_Routing_Then_Returns_Null()
    {
        // Given: Empty agent registry
        var registry = CreateInMemoryRegistry();
        var orchestrator = CreateOrchestrator(registry);
        var request = CreateRequest("test request");

        // When: Routing a request
        var agentId = await orchestrator.RouteRequestAsync(request);

        // Then: Returns null (no agent available)
        Assert.Null(agentId);
    }

    [Fact]
    public async Task Given_Valid_Request_When_Creating_Plan_Then_Returns_Plan_With_Tasks()
    {
        // Given: Orchestrator and valid request
        var registry = CreateInMemoryRegistry();
        await registry.RegisterAsync(CreateAgentDefinition("agent1"));
        
        var orchestrator = CreateOrchestrator(registry);
        var request = CreateRequest("analyze code");
        var context = CreateExecutionContext();

        // When: Creating a plan
        var plan = await orchestrator.CreatePlanAsync(request, context);

        // Then: Returns plan with tasks
        Assert.NotNull(plan);
        Assert.NotEmpty(plan.Tasks);
        Assert.Equal(request.CorrelationId, plan.CorrelationId);
        Assert.Equal(PlanStatus.Created, plan.Status);
    }

    [Fact]
    public async Task Given_Budget_Exceeded_When_Executing_Plan_Then_Fails_Plan()
    {
        // Given: Plan and context with exceeded budget
        var registry = CreateInMemoryRegistry();
        var orchestrator = CreateOrchestrator(registry);
        
        var plan = new Plan
        {
            PlanId = "plan1",
            CorrelationId = "corr1",
            Description = "Test plan",
            Tasks = new List<TaskNode>
            {
                new() { TaskId = "task1", Description = "Test task" }
            }
        };

        var context = new ExecutionContext
        {
            CorrelationId = "corr1",
            Budget = new Budget { MaxTokens = 100 },
            TokensConsumed = 101 // Already exceeded
        };

        // When: Executing the plan
        var result = await orchestrator.ExecutePlanAsync(plan, context);

        // Then: Plan fails due to budget
        Assert.Equal(PlanStatus.Failed, result.Status);
    }

    // Helper methods

    private static InMemoryAgentRegistry CreateInMemoryRegistry()
    {
        return new InMemoryAgentRegistry();
    }

    private static SwarmOrchestrator CreateOrchestrator(IAgentRegistry registry)
    {
        var factory = new MockAgentFactory();
        return new SwarmOrchestrator(registry, factory, NullLogger<SwarmOrchestrator>.Instance);
    }

    private static AgentDefinition CreateAgentDefinition(string id, bool isOrchestrator = false)
    {
        return new AgentDefinition
        {
            Id = new AgentId(id),
            Name = $"Agent {id}",
            IsOrchestrator = isOrchestrator,
            Capabilities = new[] { "test-capability" }
        };
    }

    private static RequestEnvelope CreateRequest(string payload)
    {
        return new RequestEnvelope
        {
            CorrelationId = Guid.NewGuid().ToString(),
            Source = "test",
            Payload = payload
        };
    }

    private static ExecutionContext CreateExecutionContext()
    {
        return new ExecutionContext
        {
            CorrelationId = Guid.NewGuid().ToString(),
            Budget = Budget.Default
        };
    }

    // Mock implementations for testing

    private class InMemoryAgentRegistry : IAgentRegistry
    {
        private readonly Dictionary<AgentId, AgentDefinition> _agents = new();

        public ValueTask<AgentDefinition?> GetDefinitionAsync(AgentId agentId, CancellationToken cancellationToken = default)
        {
            _agents.TryGetValue(agentId, out var def);
            return ValueTask.FromResult(def);
        }

        public ValueTask<IReadOnlyList<AgentDefinition>> GetAllDefinitionsAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyList<AgentDefinition>>(_agents.Values.ToList());
        }

        public ValueTask<IReadOnlyList<AgentDefinition>> FindByCapabilityAsync(string capability, CancellationToken cancellationToken = default)
        {
            var matches = _agents.Values.Where(d => d.Capabilities.Contains(capability)).ToList();
            return ValueTask.FromResult<IReadOnlyList<AgentDefinition>>(matches);
        }

        public ValueTask RegisterAsync(AgentDefinition definition, CancellationToken cancellationToken = default)
        {
            _agents[definition.Id] = definition;
            return ValueTask.CompletedTask;
        }

        public ValueTask UnregisterAsync(AgentId agentId, CancellationToken cancellationToken = default)
        {
            _agents.Remove(agentId);
            return ValueTask.CompletedTask;
        }
    }

    private class MockAgentFactory : IAgentFactory
    {
        public ValueTask<IAgent> CreateAgentAsync(AgentDefinition definition, CancellationToken cancellationToken = default)
        {
            var agent = new MockAgent(definition);
            return ValueTask.FromResult<IAgent>(agent);
        }
    }

    private class MockAgent(AgentDefinition definition) : IAgent
    {
        public AgentId Id => Definition.Id;
        public AgentDefinition Definition { get; } = definition;

        public ValueTask<AgentResponse> ExecuteAsync(string input, ExecutionContext context, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new AgentResponse
            {
                Content = $"Processed: {input}",
                IsSuccess = true
            });
        }
    }
}

