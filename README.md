# Delgato - Semantic Kernel Agent Swarm Platform

**Status**: MVP Implementation Started (Milestone 1)

## Overview

Delgato is a .NET 10-based agent swarm orchestration platform built on Microsoft Semantic Kernel. It enables dynamic, multi-agent collaboration through declarative YAML/JSON configuration and fluent C# APIs.

## Architecture

### Core Components

```
┌─────────────────────────────────────────────────────────────┐
│                    Transport Layer                           │
│  (HTTP API, MCP, Filesystem, Service Bus, MQTT)            │
└──────────────────────┬──────────────────────────────────────┘
                       │ RequestEnvelope
┌──────────────────────▼──────────────────────────────────────┐
│                   Orchestrator                               │
│  - Request Routing                                           │
│  - Plan Creation & Decomposition                            │
│  - Budget & Policy Enforcement                              │
└──────────────────────┬──────────────────────────────────────┘
                       │ Plan / TaskNode
┌──────────────────────▼──────────────────────────────────────┐
│                 Agent Registry                               │
│  - Load agent definitions (YAML/JSON)                       │
│  - Capability-based agent discovery                         │
└──────────────────────┬──────────────────────────────────────┘
                       │ AgentDefinition
┌──────────────────────▼──────────────────────────────────────┐
│                  Agent Factory                               │
│  - Create Semantic Kernel agents                            │
│  - Configure model providers (OpenAI, Azure, etc.)          │
└──────────────────────┬──────────────────────────────────────┘
                       │ IAgent
┌──────────────────────▼──────────────────────────────────────┐
│              Semantic Kernel Agent                           │
│  - Execute with LLM                                          │
│  - Tool invocation                                           │
│  - Budget tracking                                           │
└──────────────────────────────────────────────────────────────┘
```

### Project Structure

```
Delgato/
├── Delgato.Core/              # Domain models, abstractions, interfaces
├── Delgato.Configuration/     # YAML/JSON DSL parsing, fluent builders
├── Delgato.Agents/            # Agent implementations, registry, factory
├── Delgato.Orchestration/     # Orchestrator, planner, router
├── Delgato.Tools/             # Tool abstractions, SK integration
├── Delgato.Transports/        # Transport adapters (HTTP, MCP, etc.)
├── Delgato.Policies/          # Budget enforcement, guardrails
├── Delgato.Tests/             # TinyBDD specs and tests
├── Delgato/                   # Main host application
└── agents/                    # Agent definition files (YAML)
    ├── orchestrator.yaml
    ├── repo-analyzer.yaml
    ├── mcp-tooling.yaml
    └── prompts/
```

## Agent Definition DSL

### YAML Schema

Agents are defined using declarative YAML files:

```yaml
version: "1.0"
agents:
  - id: my-agent
    name: My Agent
    description: Agent description
    
    # Prompt can be inline or referenced
    prompt: |
      You are a helpful assistant...
    
    # Or use promptRef
    promptRef:
      source: file  # or "url", "inline"
      value: ./prompts/my-agent.txt
    
    # Model configuration
    model:
      provider: openai
      modelId: gpt-4
      temperature: 0.7
      maxTokens: 4000
    
    # Capabilities for routing
    capabilities:
      - code-analysis
      - debugging
    
    # Tool permissions
    allowedTools:
      - read_file
      - search_code
    
    # Budget constraints
    budget:
      maxTokens: 100000
      maxToolCalls: 50
      maxDepth: 3
      maxDurationSeconds: 300
      maxCost: 2.0
    
    # Policies
    policy:
      deniedTools:
        - delete_file
      requiredCapabilities:
        - code-analysis
      auditAllToolCalls: true
      allowRecursion: false
    
    # Metadata
    metadata:
      version: "1.0"
      author: "team"
```

### Fluent API (C#)

The same configuration can be built programmatically:

```csharp
using Delgato.Configuration.Builders;

var agent = new AgentDefinitionBuilder()
    .WithId("my-agent")
    .WithName("My Agent")
    .WithDescription("Agent description")
    .WithPromptFromFile("./prompts/my-agent.txt")
    .WithModel(m => m
        .WithProvider("openai")
        .WithModelId("gpt-4")
        .WithTemperature(0.7)
        .WithMaxTokens(4000))
    .WithCapabilities("code-analysis", "debugging")
    .WithAllowedTools("read_file", "search_code")
    .WithBudget(b => b
        .WithMaxTokens(100000)
        .WithMaxToolCalls(50)
        .WithMaxDepth(3)
        .WithMaxDuration(TimeSpan.FromMinutes(5)))
    .WithPolicy(p => p
        .DenyTool("delete_file")
        .RequireCapability("code-analysis")
        .WithAuditAllToolCalls()
        .WithAllowRecursion(false))
    .WithMetadata("version", "1.0")
    .Build();
```

## Execution Flow

### 1. Request Ingestion

```
Transport Adapter → RequestEnvelope
{
  correlationId: "uuid",
  source: "http",
  payload: { ... },
  metadata: { ... }
}
```

### 2. Routing

```csharp
// Orchestrator determines best agent
var agentId = await orchestrator.RouteRequestAsync(request);

// Routing strategies:
// - Capability matching
// - Embeddings (v1)
// - Rule-based
// - LLM-based (v1)
```

### 3. Planning

```csharp
// Create execution plan (DAG)
var plan = await orchestrator.CreatePlanAsync(request, context);

// Plan structure:
Plan {
  planId: "uuid",
  tasks: [
    { taskId, description, dependencies, assignedAgent },
    ...
  ],
  depth: 0
}
```

### 4. Execution

```csharp
// Execute plan with budget enforcement
var executedPlan = await orchestrator.ExecutePlanAsync(plan, context);

// Budget tracking:
context.TokensConsumed += agent.TokensUsed;
context.ToolCallsExecuted += agent.ToolCalls;

if (!context.IsWithinBudget()) {
  // Halt execution
}
```

### 5. Response

```
Response → Transport Adapter → Client
{
  planId, status, tasks, tokensUsed, toolCalls
}
```

## Guardrails & Safety

### Budget Enforcement

```csharp
public sealed record Budget
{
    int? MaxTokens;         // Total token limit
    int? MaxToolCalls;      // Max tool invocations
    int? MaxDepth;          // Recursion depth limit
    TimeSpan? MaxDuration;  // Time limit
    decimal? MaxCost;       // Cost limit
}
```

### Policy Enforcement

```yaml
policy:
  deniedTools:           # Blocked tools
    - delete_file
    - execute_shell
  requiredCapabilities:  # Required agent caps
    - safe-execution
  auditAllToolCalls: true
  allowRecursion: false
```

### Execution Context

Tracks resource consumption in real-time:

```csharp
context.TokensConsumed += tokensUsed;
context.ToolCallsExecuted++;
context.CurrentDepth++;

if (!context.IsWithinBudget()) {
  return Error("Budget exceeded");
}
```

## Configuration

### Environment Variables

```bash
# OpenAI
OPENAI_API_KEY=sk-...

# Azure OpenAI
AZURE_OPENAI_ENDPOINT=https://...
AZURE_OPENAI_API_KEY=...

# Agent directory
AGENT_DIRECTORY=./agents
```

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Delgato": "Debug"
    }
  },
  "SwarmOptions": {
    "AgentDirectory": "./agents"
  }
}
```

## Usage Examples

### Start the System

```bash
dotnet run --project Delgato
```

### Submit Request (HTTP)

```csharp
// Via HttpTransportAdapter
var response = await httpAdapter.SubmitRequestAsync(
    payload: new { task = "Analyze repository structure" },
    metadata: new Dictionary<string, string> {
        ["userId"] = "user123"
    }
);
```

## Milestones

### ✅ MVP (Current)

- [x] Core domain models
- [x] DSL schema (YAML/JSON)
- [x] Fluent API builders
- [x] File-based agent registry
- [x] Basic orchestrator (simple routing + planning)
- [x] Semantic Kernel agent implementation
- [x] HTTP transport adapter
- [x] Budget tracking
- [x] Example agent definitions (orchestrator, repo-analyzer, mcp-tooling)

### 🔲 v1 (Next)

- [ ] LLM-based planning (SK Planner)
- [ ] Advanced routing (embeddings, semantic search)
- [ ] Tool registry & permission enforcement
- [ ] MCP transport adapter
- [ ] Filesystem watch transport
- [ ] OpenTelemetry instrumentation
- [ ] Policy enforcement engine
- [ ] Comprehensive test suite (TinyBDD)
- [ ] Prompt template system
- [ ] Agent memory & state persistence

### 🔲 vNext (Future)

- [ ] Service Bus & MQTT transports
- [ ] Multi-node scale-out
- [ ] Distributed locks & coordination
- [ ] Durable orchestration (Durable Task Framework)
- [ ] Agent marketplace
- [ ] Web UI dashboard
- [ ] Advanced observability (metrics, traces, logs)

## Testing Strategy

### TinyBDD Scenarios

Tests use TinyBDD-style Given/When/Then scenarios:

```csharp
[Fact]
public async Task Orchestrator_Routes_To_Capable_Agent()
{
    // Given: Multiple agents with different capabilities
    var agents = new[]
    {
        AgentWithCapabilities("agent1", "code-analysis"),
        AgentWithCapabilities("agent2", "data-processing")
    };
    await LoadAgents(agents);

    // When: Request requires code analysis
    var request = CreateRequest("Analyze this C# code");
    var agentId = await orchestrator.RouteRequestAsync(request);

    // Then: Routes to code-analysis agent
    Assert.Equal("agent1", agentId.Value);
}
```

### Test Pyramid

```
       /\
      /E2E\         End-to-end (full system)
     /------\
    /Integ. \       Integration (agent + orchestrator)
   /----------\
  /   Unit     \    Unit (individual components)
 /--------------\
```

## Design Patterns

### Core Patterns

- **Builder Pattern**: Fluent API for agent definition
- **Factory Pattern**: Agent creation (`IAgentFactory`)
- **Registry Pattern**: Agent discovery (`IAgentRegistry`)
- **Strategy Pattern**: Transport adapters (`ITransportAdapter`)
- **Chain of Responsibility**: Tool execution pipeline
- **Command Pattern**: Task execution
- **Observer Pattern**: Event-driven transport
- **Repository Pattern**: Agent definition storage

### PatternKit Integration

Delgato follows PatternKit conventions:
- Fluent builders with progressive disclosure
- Strongly-typed IDs (`AgentId`)
- Immutable records for data transfer
- Async-first with `ValueTask`
- Extension methods for composability

## OpenTelemetry

### Span Names

```
delgato.orchestration.route_request
delgato.orchestration.create_plan
delgato.orchestration.execute_plan
delgato.agent.execute
delgato.tool.invoke
delgato.transport.receive
delgato.transport.send_response
```

### Metrics

```
delgato_requests_total
delgato_plans_created_total
delgato_tasks_executed_total
delgato_tokens_consumed_total
delgato_tool_calls_total
delgato_budget_exceeded_total
delgato_execution_duration_seconds
```

## Contributing

### Development Workflow

1. Create agent definitions in `agents/`
2. Implement tests in `Delgato.Tests/`
3. Add features to appropriate project
4. Update README documentation
5. Run tests: `dotnet test`
6. Submit PR

### Code Style

- Use nullable reference types
- Prefer `ValueTask` for hot paths
- Use `sealed` for classes that shouldn't be inherited
- Follow async/await best practices
- Use structured logging

## License

MIT License - See LICENSE file

## Support

- Documentation: [docs/](docs/)
- Issues: GitHub Issues
- Discussions: GitHub Discussions

