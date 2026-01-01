using Delgato.Core;
using Delgato.Core.Abstractions;
using Delgato.Providers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Delgato.Tests.Behaviors;

/// <summary>
/// TinyBDD behavior tests for Agent Provider functionality.
/// </summary>
public class AgentProviderBehaviors
{
    #region Given/When/Then Helpers

    private IProviderRegistry? _registry;
    private IAgentProvider? _provider;
    private ProviderHealthResult? _healthResult;
    private AgentResponse? _response;
    private Exception? _exception;

    private void Given_A_Provider_Registry()
    {
        _registry = new ProviderRegistry(NullLogger<ProviderRegistry>.Instance);
    }

    private void Given_A_Mock_Provider()
    {
        _provider = new MockAgentProvider("mock", "Mock Provider");
    }

    private void When_The_Provider_Is_Registered()
    {
        _registry!.Register(_provider!);
    }

    private void When_Health_Is_Checked()
    {
        try
        {
            _healthResult = _provider!.ValidateConnectionAsync().Result;
        }
        catch (Exception ex)
        {
            _exception = ex;
        }
    }

    private void When_A_Request_Is_Executed()
    {
        try
        {
            var request = new AgentExecutionRequest
            {
                Input = "Hello, world!",
                AgentDefinition = CreateTestAgentDefinition()
            };
            var context = CreateTestContext();
            _response = _provider!.ExecuteAsync(request, context).Result;
        }
        catch (Exception ex)
        {
            _exception = ex;
        }
    }

    #endregion

    [Fact]
    public void Provider_Registry_Should_Register_Providers()
    {
        // Given
        Given_A_Provider_Registry();
        Given_A_Mock_Provider();

        // When
        When_The_Provider_Is_Registered();

        // Then
        Assert.Contains(_registry!.GetAll(), p => p.ProviderId == "mock");
    }

    [Fact]
    public void Provider_Registry_Should_Find_Provider_By_Id()
    {
        // Given
        Given_A_Provider_Registry();
        Given_A_Mock_Provider();
        When_The_Provider_Is_Registered();

        // When
        var found = _registry!.GetById("mock");

        // Then
        Assert.NotNull(found);
        Assert.Equal("Mock Provider", found.DisplayName);
    }

    [Fact]
    public void Provider_Registry_Should_Find_Providers_By_Capability()
    {
        // Given
        Given_A_Provider_Registry();
        Given_A_Mock_Provider();
        When_The_Provider_Is_Registered();

        // When
        var found = _registry!.GetByCapability("chat");

        // Then
        Assert.Single(found);
    }

    [Fact]
    public void Provider_Should_Return_Health_Status()
    {
        // Given
        Given_A_Mock_Provider();

        // When
        When_Health_Is_Checked();

        // Then
        Assert.NotNull(_healthResult);
        Assert.True(_healthResult.IsHealthy);
    }

    [Fact]
    public void Provider_Should_Execute_Requests()
    {
        // Given
        Given_A_Mock_Provider();

        // When
        When_A_Request_Is_Executed();

        // Then
        Assert.NotNull(_response);
        Assert.True(_response.IsSuccess);
        Assert.NotEmpty(_response.Content);
    }

    [Fact]
    public void Provider_Should_Track_Token_Usage()
    {
        // Given
        Given_A_Mock_Provider();

        // When
        When_A_Request_Is_Executed();

        // Then
        Assert.NotNull(_response);
        Assert.True(_response.TokensUsed > 0);
    }

    private AgentDefinition CreateTestAgentDefinition()
    {
        return new AgentDefinition
        {
            Id = new AgentId("test-agent"),
            Name = "Test Agent",
            Model = new ModelConfiguration
            {
                Provider = "mock",
                ModelId = "mock-model"
            }
        };
    }

    private ExecutionContext CreateTestContext()
    {
        return new ExecutionContext
        {
            CorrelationId = Guid.NewGuid().ToString(),
            Budget = new Budget { MaxTokens = 1000 }
        };
    }
}

/// <summary>
/// Mock agent provider for testing.
/// </summary>
internal class MockAgentProvider : IAgentProvider
{
    public MockAgentProvider(string id, string displayName)
    {
        ProviderId = id;
        DisplayName = displayName;
    }

    public string ProviderId { get; }
    public string DisplayName { get; }
    public IReadOnlyList<string> SupportedModels => new[] { "mock-model" };
    public IReadOnlyList<string> Capabilities => new[] { "chat", "streaming" };

    public Task<ProviderHealthResult> ValidateConnectionAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new ProviderHealthResult
        {
            IsHealthy = true,
            Status = "OK",
            Latency = TimeSpan.FromMilliseconds(10)
        });
    }

    public Task<AgentResponse> ExecuteAsync(AgentExecutionRequest request, ExecutionContext context, CancellationToken ct = default)
    {
        return Task.FromResult(new AgentResponse
        {
            Content = $"Mock response to: {request.Input}",
            IsSuccess = true,
            TokensUsed = 50
        });
    }

    public async IAsyncEnumerable<AgentStreamEvent> StreamAsync(AgentExecutionRequest request, ExecutionContext context, CancellationToken ct = default)
    {
        yield return new AgentStreamEvent
        {
            Type = AgentStreamEventType.ContentDelta,
            Content = "Mock "
        };
        yield return new AgentStreamEvent
        {
            Type = AgentStreamEventType.ContentDelta,
            Content = "streaming "
        };
        yield return new AgentStreamEvent
        {
            Type = AgentStreamEventType.ContentDelta,
            Content = "response"
        };
        yield return new AgentStreamEvent
        {
            Type = AgentStreamEventType.Complete,
            FinalResponse = new AgentResponse
            {
                Content = "Mock streaming response",
                IsSuccess = true,
                TokensUsed = 30
            }
        };
    }

    public Task<IReadOnlyList<ToolDefinition>> GetAvailableToolsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<ToolDefinition>>(Array.Empty<ToolDefinition>());
    }
}
