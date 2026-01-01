# Delgato Architecture

This document describes the architecture of the Delgato Enterprise Agent Swarm Ecosystem.

## Overview

Delgato follows a layered, modular architecture designed for extensibility, testability, and production deployment.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              DELGATO ECOSYSTEM                               │
├─────────────────────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐              │
│  │   Blazor Web    │  │      CLI        │  │   REST/gRPC     │  Interfaces  │
│  │   Dashboard     │  │   Application   │  │      APIs       │              │
│  └────────┬────────┘  └────────┬────────┘  └────────┬────────┘              │
│           │                    │                    │                        │
│  ─────────┴────────────────────┴────────────────────┴──────────────────────  │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────────┐ │
│  │                        ORCHESTRATION LAYER                              │ │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌─────────────┐  │ │
│  │  │ Tree         │  │ Agent Tree   │  │ Plan         │  │ Capability  │  │ │
│  │  │ Orchestrator │  │ Manager      │  │ Executor     │  │ Router      │  │ │
│  │  └──────────────┘  └──────────────┘  └──────────────┘  └─────────────┘  │ │
│  └─────────────────────────────────────────────────────────────────────────┘ │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────────┐ │
│  │                          AGENT LAYER                                    │ │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  │ │
│  │  │ Claude   │  │ OpenAI   │  │ Gemini   │  │ Ollama   │  │ Custom   │  │ │
│  │  │ Provider │  │ Provider │  │ Provider │  │ Provider │  │ Provider │  │ │
│  │  └──────────┘  └──────────┘  └──────────┘  └──────────┘  └──────────┘  │ │
│  └─────────────────────────────────────────────────────────────────────────┘ │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────────┐ │
│  │                      ENTERPRISE SERVICES                                │ │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  │ │
│  │  │ Audit    │  │ Cost     │  │ Compliance│  │ Health   │  │ Telemetry│  │ │
│  │  │ Service  │  │ Tracker  │  │ Engine   │  │ Monitor  │  │ Service  │  │ │
│  │  └──────────┘  └──────────┘  └──────────┘  └──────────┘  └──────────┘  │ │
│  └─────────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Core Components

### Delgato.Core

The foundational layer containing:

- **Domain Models**: `AgentDefinition`, `ExecutionContext`, `Plan`, `TaskNode`
- **Abstractions**: `IAgent`, `IAgentProvider`, `IOrchestrator`, `IAgentRegistry`
- **Agent Tree**: `AgentNode`, `AgentTree` for N-tree orchestration
- **Governance**: Audit events, cost tracking, compliance interfaces

### Delgato.Providers

Pluggable LLM provider implementations:

- **ClaudeProvider**: Anthropic Claude integration with streaming and tools
- **OpenAIProvider**: OpenAI GPT models including o1 and o3
- **GeminiProvider**: Google Gemini Pro, Ultra, and Flash
- **ProviderRegistry**: Dynamic provider registration and discovery

### Delgato.Orchestration

Orchestration engine:

- **TreeOrchestrator**: N-tree based hierarchical orchestration
- **Plan Creation**: Request decomposition and task planning
- **Plan Execution**: Parallel and sequential task execution
- **Capability Routing**: Route requests to capable agents

### Delgato.Governance

Enterprise governance features:

- **InMemoryAuditService**: Audit trail for all operations
- **InMemoryCostTracker**: Cost monitoring and budget enforcement
- **ComplianceEngine**: Policy validation and compliance

### Delgato.Cli

Command-line interface:

- **Agent Commands**: List, create, run, validate agents
- **Swarm Commands**: Execute requests, view trees
- **Config Commands**: Configuration management
- **Interactive Mode**: REPL for agent interaction

### Delgato.Dashboard.Web

Blazor web dashboard:

- **Agents Page**: Full CRUD for agent management
- **Swarm Page**: Execution tree visualization
- **Providers Page**: Provider health monitoring
- **Audit Page**: Searchable audit log viewer

## Design Patterns

### Factory Pattern
Used for creating agents from definitions:
```csharp
public interface IAgentFactory
{
    ValueTask<IAgent> CreateAgentAsync(AgentDefinition definition, CancellationToken ct);
}
```

### Registry Pattern
Central registries for agents and providers:
```csharp
public interface IAgentRegistry
{
    Task<IReadOnlyList<AgentDefinition>> GetAllDefinitionsAsync(CancellationToken ct);
}
```

### Strategy Pattern
Different providers implement the same interface:
```csharp
public interface IAgentProvider
{
    Task<AgentResponse> ExecuteAsync(AgentExecutionRequest request, ExecutionContext context, CancellationToken ct);
}
```

### Builder Pattern
Fluent API for agent configuration:
```csharp
var agent = new AgentDefinitionBuilder()
    .WithId("my-agent")
    .WithName("My Agent")
    .WithModel("anthropic", "claude-3-5-sonnet")
    .Build();
```

## N-Tree Orchestration Model

The N-tree model allows agents to spawn child agents recursively:

```
                    ┌──────────────┐
                    │   Root       │
                    │ Orchestrator │
                    └──────┬───────┘
                           │
           ┌───────────────┼───────────────┐
           │               │               │
    ┌──────┴──────┐ ┌──────┴──────┐ ┌──────┴──────┐
    │  Analyzer   │ │   Coder     │ │  Reviewer   │
    └──────┬──────┘ └──────┬──────┘ └─────────────┘
           │               │
     ┌─────┴─────┐   ┌─────┴─────┐
     │  Detail1  │   │   Detail2  │
     └───────────┘   └───────────┘
```

Key features:
- Maximum depth enforcement
- Maximum node count limits
- Budget accumulation across tree
- Parallel and sequential execution
- Tree pruning and cleanup

## Data Flow

1. **Request Ingestion**: Request received via CLI, Dashboard, or API
2. **Routing**: Request routed to appropriate agent/orchestrator
3. **Planning**: Orchestrator decomposes request into tasks
4. **Execution**: Tasks executed by agents (potentially spawning children)
5. **Aggregation**: Results aggregated up the tree
6. **Response**: Final response returned to caller

## Security Model

- **API Key Management**: Environment variables or secure vaults
- **Audit Trail**: All operations logged with correlation IDs
- **Budget Enforcement**: Token, cost, and depth limits
- **Policy Validation**: Compliance checks before execution

## Extensibility Points

1. **Custom Providers**: Implement `IAgentProvider`
2. **Custom Transports**: Implement `ITransportAdapter`
3. **Custom Tools**: Register with `IToolRegistry`
4. **Custom Policies**: Implement compliance rules
5. **Custom Persistence**: Replace in-memory stores
