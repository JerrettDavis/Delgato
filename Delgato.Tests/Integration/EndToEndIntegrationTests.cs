using Delgato.Agents;
using Delgato.Core;
using Delgato.Core.Abstractions;
using Delgato.Orchestration;
using Delgato.Transports.Http;
using Microsoft.Extensions.Logging.Abstractions;
using ExecutionContext = Delgato.Core.ExecutionContext;
using TaskStatus = Delgato.Core.TaskStatus;

namespace Delgato.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the complete request flow.
/// 
/// Scenario: Complete request processing from ingress to response
/// </summary>
public class EndToEndIntegrationTests
{
    [Fact]
    public async Task Given_Valid_Request_When_Processing_End_To_End_Then_Returns_Successful_Response()
    {
        // Given: A fully configured system with agents, orchestrator, and transport
        var registry = new InMemoryAgentRegistry();
        await registry.RegisterAsync(CreateOrchestratorAgent());
        await registry.RegisterAsync(CreateWorkerAgent());

        var factory = new MockAgentFactory();
        var orchestrator = new SwarmOrchestrator(registry, factory, NullLogger<SwarmOrchestrator>.Instance);
        var transport = new HttpTransportAdapter(NullLogger<HttpTransportAdapter>.Instance);

        await transport.StartAsync();

        // When: Submitting a request through the transport
        var submitTask = transport.SubmitRequestAsync(
            payload: new { task = "Process this request" },
            metadata: new Dictionary<string, string> { ["userId"] = "test-user" });

        // Process the request (simulating the worker)
        await foreach (var request in transport.ReceiveAsync().Take(1))
        {
            var context = new ExecutionContext
            {
                CorrelationId = request.CorrelationId,
                Budget = Budget.Default
            };

            var agentId = await orchestrator.RouteRequestAsync(request);
            Assert.NotNull(agentId);

            var plan = await orchestrator.CreatePlanAsync(request, context);
            Assert.NotNull(plan);
            Assert.NotEmpty(plan.Tasks);

            var executedPlan = await orchestrator.ExecutePlanAsync(plan, context);
            Assert.Equal(PlanStatus.Completed, executedPlan.Status);

            var response = new
            {
                planId = executedPlan.PlanId,
                status = executedPlan.Status.ToString(),
                tasks = executedPlan.Tasks.Select(t => new { t.TaskId, t.Status, t.Result }).ToList()
            };

            await transport.SendResponseAsync(request.CorrelationId, response);
        }

        // Then: Response is received successfully
        var result = await submitTask;
        Assert.NotNull(result);

        await transport.StopAsync();
    }

    [Fact]
    public async Task Given_Budget_Exceeded_When_Processing_Then_Plan_Fails_Gracefully()
    {
        // Given: System with restrictive budget
        var registry = new InMemoryAgentRegistry();
        await registry.RegisterAsync(CreateOrchestratorAgent());

        var factory = new MockAgentFactory();
        var orchestrator = new SwarmOrchestrator(registry, factory, NullLogger<SwarmOrchestrator>.Instance);

        var request = new RequestEnvelope
        {
            CorrelationId = Guid.NewGuid().ToString(),
            Source = "test",
            Payload = new { task = "test" }
        };

        var context = new ExecutionContext
        {
            CorrelationId = request.CorrelationId,
            Budget = new Budget { MaxTokens = 10 },
            TokensConsumed = 11 // Already exceeded
        };

        // When: Attempting to execute a plan
        var plan = await orchestrator.CreatePlanAsync(request, context);
        var executedPlan = await orchestrator.ExecutePlanAsync(plan, context);

        // Then: Plan fails due to budget constraints
        Assert.Equal(PlanStatus.Failed, executedPlan.Status);
    }

    [Fact]
    public async Task Given_Multiple_Agents_When_Routing_Then_Selects_Correct_Agent_By_Capability()
    {
        // Given: Multiple agents with different capabilities
        var registry = new InMemoryAgentRegistry();
        await registry.RegisterAsync(new AgentDefinition
        {
            Id = new AgentId("orchestrator"),
            Name = "Orchestrator",
            IsOrchestrator = true,
            Capabilities = new[] { "orchestration" }
        });
        await registry.RegisterAsync(new AgentDefinition
        {
            Id = new AgentId("specialist"),
            Name = "Specialist",
            IsOrchestrator = false,
            Capabilities = new[] { "code-analysis" }
        });

        var factory = new MockAgentFactory();
        var orchestrator = new SwarmOrchestrator(registry, factory, NullLogger<SwarmOrchestrator>.Instance);

        var request = new RequestEnvelope
        {
            CorrelationId = Guid.NewGuid().ToString(),
            Source = "test",
            Payload = new { task = "test" }
        };

        // When: Routing the request
        var agentId = await orchestrator.RouteRequestAsync(request);

        // Then: Routes to orchestrator (preferred)
        Assert.NotNull(agentId);
        Assert.Equal("orchestrator", agentId.Value.Value);
    }

    [Fact]
    public async Task Given_Failing_Task_When_Executing_Plan_Then_Captures_Error_In_Task()
    {
        // Given: A plan that will fail
        var registry = new InMemoryAgentRegistry();
        await registry.RegisterAsync(CreateFailingAgent());

        var factory = new FailingAgentFactory();
        var orchestrator = new SwarmOrchestrator(registry, factory, NullLogger<SwarmOrchestrator>.Instance);

        var request = new RequestEnvelope
        {
            CorrelationId = Guid.NewGuid().ToString(),
            Source = "test",
            Payload = new { task = "test" }
        };

        var context = new ExecutionContext
        {
            CorrelationId = request.CorrelationId,
            Budget = Budget.Default
        };

        // When: Executing the plan
        var plan = await orchestrator.CreatePlanAsync(request, context);
        var executedPlan = await orchestrator.ExecutePlanAsync(plan, context);

        // Then: Plan shows failure with error message
        Assert.Contains(executedPlan.Tasks, t => t.Status == TaskStatus.Failed);
        Assert.Contains(executedPlan.Tasks, t => !string.IsNullOrEmpty(t.ErrorMessage));
    }

    // Helper methods and mock implementations

    private static AgentDefinition CreateOrchestratorAgent()
    {
        return new AgentDefinition
        {
            Id = new AgentId("orchestrator"),
            Name = "Test Orchestrator",
            IsOrchestrator = true,
            Capabilities = new[] { "orchestration", "planning" }
        };
    }

    private static AgentDefinition CreateWorkerAgent()
    {
        return new AgentDefinition
        {
            Id = new AgentId("worker"),
            Name = "Test Worker",
            IsOrchestrator = false,
            Capabilities = new[] { "task-execution" }
        };
    }

    private static AgentDefinition CreateFailingAgent()
    {
        return new AgentDefinition
        {
            Id = new AgentId("failing-agent"),
            Name = "Failing Agent",
            IsOrchestrator = false,
            Capabilities = new[] { "will-fail" }
        };
    }

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

    private class MockAgent : IAgent
    {
        public AgentId Id => Definition.Id;
        public AgentDefinition Definition { get; }

        public MockAgent(AgentDefinition definition)
        {
            Definition = definition;
        }

        public ValueTask<AgentResponse> ExecuteAsync(string input, ExecutionContext context, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new AgentResponse
            {
                Content = $"Mock response for: {input}",
                IsSuccess = true,
                TokensUsed = 50,
                ToolCallsExecuted = 0
            });
        }
    }

    private class FailingAgentFactory : IAgentFactory
    {
        public ValueTask<IAgent> CreateAgentAsync(AgentDefinition definition, CancellationToken cancellationToken = default)
        {
            var agent = new FailingAgent(definition);
            return ValueTask.FromResult<IAgent>(agent);
        }
    }

    private class FailingAgent : IAgent
    {
        public AgentId Id => Definition.Id;
        public AgentDefinition Definition { get; }

        public FailingAgent(AgentDefinition definition)
        {
            Definition = definition;
        }

        public ValueTask<AgentResponse> ExecuteAsync(string input, ExecutionContext context, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new AgentResponse
            {
                Content = string.Empty,
                IsSuccess = false,
                ErrorMessage = "Simulated agent failure"
            });
        }
    }
}

