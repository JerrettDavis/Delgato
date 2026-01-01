using Delgato.Core;
using Delgato.Core.Abstractions;
using Delgato.Core.AgentTree;
using Delgato.Orchestration;
using Microsoft.Extensions.Logging.Abstractions;
using ExecutionContext = Delgato.Core.ExecutionContext;

namespace Delgato.Tests.Behaviors;

/// <summary>
/// TinyBDD behavior tests for Orchestrator functionality.
/// </summary>
public class OrchestratorBehaviors
{
    private IOrchestrator? _orchestrator;
    private IAgentRegistry? _registry;
    private IAgentFactory? _factory;
    private IProviderRegistry? _providerRegistry;
    private RequestEnvelope? _request;
    private ExecutionContext? _context;
    private Plan? _plan;
    private AgentId? _routedAgentId;

    #region Given/When/Then Helpers

    private void Given_An_Orchestrator()
    {
        _registry = new MockAgentRegistry();
        _factory = new MockAgentFactory();
        _providerRegistry = new MockProviderRegistry();
        _orchestrator = new TreeOrchestrator(
            _registry,
            _factory,
            _providerRegistry,
            NullLogger<TreeOrchestrator>.Instance);
    }

    private void Given_A_Request()
    {
        _request = new RequestEnvelope
        {
            CorrelationId = Guid.NewGuid().ToString(),
            Source = "test",
            Payload = "Test request",
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    private void Given_An_Execution_Context()
    {
        _context = new ExecutionContext
        {
            CorrelationId = _request!.CorrelationId,
            Budget = new Budget
            {
                MaxTokens = 10000,
                MaxDepth = 5,
                MaxCost = 1.0m
            }
        };
    }

    private async Task When_Plan_Is_Created()
    {
        _plan = await _orchestrator!.CreatePlanAsync(_request!, _context!);
    }

    private async Task When_Plan_Is_Executed()
    {
        _plan = await _orchestrator!.ExecutePlanAsync(_plan!, _context!);
    }

    private async Task When_Request_Is_Routed()
    {
        _routedAgentId = await _orchestrator!.RouteRequestAsync(_request!);
    }

    #endregion

    [Fact]
    public async Task Orchestrator_Should_Create_Plan_For_Request()
    {
        // Given
        Given_An_Orchestrator();
        Given_A_Request();
        Given_An_Execution_Context();

        // When
        await When_Plan_Is_Created();

        // Then
        Assert.NotNull(_plan);
        Assert.Equal(PlanStatus.Created, _plan.Status);
        Assert.NotEmpty(_plan.Tasks);
    }

    [Fact]
    public async Task Orchestrator_Should_Execute_Plan()
    {
        // Given
        Given_An_Orchestrator();
        Given_A_Request();
        Given_An_Execution_Context();
        await When_Plan_Is_Created();

        // When
        await When_Plan_Is_Executed();

        // Then
        Assert.NotNull(_plan);
        Assert.True(_plan.Status == PlanStatus.Completed || _plan.Status == PlanStatus.PartiallyCompleted);
    }

    [Fact]
    public async Task Orchestrator_Should_Route_To_Orchestrator_Agent()
    {
        // Given
        Given_An_Orchestrator();
        Given_A_Request();

        // When
        await When_Request_Is_Routed();

        // Then
        Assert.NotNull(_routedAgentId);
    }

    [Fact]
    public async Task Orchestrator_Should_Track_Tokens()
    {
        // Given
        Given_An_Orchestrator();
        Given_A_Request();
        Given_An_Execution_Context();
        await When_Plan_Is_Created();

        // When
        await When_Plan_Is_Executed();

        // Then
        Assert.True(_context!.TokensConsumed > 0);
    }

    [Fact]
    public async Task Orchestrator_Should_Enforce_Budget()
    {
        // Given
        Given_An_Orchestrator();
        Given_A_Request();
        _context = new ExecutionContext
        {
            CorrelationId = _request!.CorrelationId,
            Budget = new Budget { MaxTokens = 0 } // Immediate budget exceeded
        };
        await When_Plan_Is_Created();

        // When
        await When_Plan_Is_Executed();

        // Then
        Assert.Equal(PlanStatus.Failed, _plan!.Status);
    }

    [Fact]
    public async Task Tree_Orchestrator_Should_Track_Execution_Trees()
    {
        // Given
        Given_An_Orchestrator();
        Given_A_Request();
        Given_An_Execution_Context();
        await When_Plan_Is_Created();
        await When_Plan_Is_Executed();

        // When
        var treeOrch = _orchestrator as TreeOrchestrator;
        var summaries = treeOrch!.GetTreeSummaries();

        // Then
        Assert.NotEmpty(summaries);
    }

    [Fact]
    public async Task Tree_Orchestrator_Should_Get_Tree_By_Id()
    {
        // Given
        Given_An_Orchestrator();
        Given_A_Request();
        Given_An_Execution_Context();
        await When_Plan_Is_Created();

        // When
        var treeOrch = _orchestrator as TreeOrchestrator;
        var treeId = Guid.Parse(_plan!.PlanId);
        var tree = treeOrch!.GetTree(treeId);

        // Then
        Assert.NotNull(tree);
        Assert.Equal(_request!.CorrelationId, tree.CorrelationId);
    }
}

#region Mock Implementations

internal class MockAgentRegistry : IAgentRegistry
{
    private readonly Dictionary<AgentId, AgentDefinition> _agents = new();

    public MockAgentRegistry()
    {
        var orchestrator = new AgentDefinition
        {
            Id = new AgentId("orchestrator"),
            Name = "Orchestrator",
            IsOrchestrator = true,
            Model = new ModelConfiguration
            {
                Provider = "mock",
                ModelId = "mock-model"
            }
        };
        _agents[orchestrator.Id] = orchestrator;

        var worker = new AgentDefinition
        {
            Id = new AgentId("worker"),
            Name = "Worker",
            Capabilities = new[] { "general" }
        };
        _agents[worker.Id] = worker;
    }

    public ValueTask<AgentDefinition?> GetDefinitionAsync(AgentId id, CancellationToken ct = default)
    {
        _agents.TryGetValue(id, out var def);
        return ValueTask.FromResult(def);
    }

    public ValueTask<IReadOnlyList<AgentDefinition>> GetAllDefinitionsAsync(CancellationToken ct = default)
    {
        return ValueTask.FromResult<IReadOnlyList<AgentDefinition>>(_agents.Values.ToList());
    }

    public ValueTask<IReadOnlyList<AgentDefinition>> FindByCapabilityAsync(string capability, CancellationToken ct = default)
    {
        var result = _agents.Values.Where(a => a.Capabilities.Contains(capability)).ToList();
        return ValueTask.FromResult<IReadOnlyList<AgentDefinition>>(result);
    }

    public ValueTask RegisterAsync(AgentDefinition definition, CancellationToken ct = default)
    {
        _agents[definition.Id] = definition;
        return ValueTask.CompletedTask;
    }

    public ValueTask UnregisterAsync(AgentId id, CancellationToken ct = default)
    {
        _agents.Remove(id);
        return ValueTask.CompletedTask;
    }
}

internal class MockAgentFactory : IAgentFactory
{
    public ValueTask<IAgent> CreateAgentAsync(AgentDefinition definition, CancellationToken ct = default)
    {
        return ValueTask.FromResult<IAgent>(new MockAgent(definition));
    }
}

internal class MockAgent : IAgent
{
    public MockAgent(AgentDefinition definition)
    {
        Id = definition.Id;
        Definition = definition;
    }

    public AgentId Id { get; }
    public AgentDefinition Definition { get; }

    public ValueTask<AgentResponse> ExecuteAsync(string input, ExecutionContext context, CancellationToken ct = default)
    {
        return ValueTask.FromResult(new AgentResponse
        {
            Content = $"Mock response to: {input}",
            IsSuccess = true,
            TokensUsed = 50
        });
    }
}

internal class MockProviderRegistry : IProviderRegistry
{
    public IReadOnlyList<IAgentProvider> GetAll() => Array.Empty<IAgentProvider>();
    public IAgentProvider? GetById(string providerId) => null;
    public IReadOnlyList<IAgentProvider> GetByCapability(string capability) => Array.Empty<IAgentProvider>();
    public IReadOnlyList<IAgentProvider> GetByModel(string modelId) => Array.Empty<IAgentProvider>();
    public void Register(IAgentProvider provider) { }
    public bool Unregister(string providerId) => false;
    public Task<IReadOnlyDictionary<string, ProviderHealthResult>> CheckHealthAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<string, ProviderHealthResult>>(new Dictionary<string, ProviderHealthResult>());
}

#endregion
