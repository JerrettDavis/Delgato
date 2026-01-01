# Delgato - Enterprise Agent Swarm Ecosystem

**Status**: Production-Ready Enterprise Platform

[![Build](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com/your-org/delgato)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

## Overview

Delgato is an extensible, pluggable, secure, enterprise-grade agent swarm platform. It enables organizations to orchestrate multiple AI agents across different providers (Claude, OpenAI, Gemini, and more) in a hierarchical N-tree structure with full enterprise governance, auditing, and cost management.

### Key Features

- **Multi-Provider Support**: Seamlessly integrate with Anthropic Claude, OpenAI, Google Gemini, Ollama, and custom providers
- **N-Tree Orchestration**: Hierarchical agent spawning with configurable depth and node limits
- **Enterprise Governance**: Complete audit trails, compliance tracking, cost management, and budget enforcement
- **Dual Interface**: Full-featured CLI and Blazor web dashboard
- **Production-Ready**: DRY, SOLID architecture with comprehensive TinyBDD testing
- **Extensible**: Plugin architecture for providers, transports, and tools

## Quick Start

### Prerequisites

- .NET 10.0 SDK or later
- API keys for your chosen LLM providers

### Installation

```bash
# Clone the repository
git clone https://github.com/your-org/delgato.git
cd delgato

# Set up API keys
export ANTHROPIC_API_KEY=your-key
export OPENAI_API_KEY=your-key

# Build and run
dotnet build
dotnet run --project Delgato.Dashboard.Web
```

### CLI Quick Start

```bash
# Install CLI
dotnet tool install -g delgato

# List agents
delgato agent list

# Run an agent
delgato agent run orchestrator "Analyze this codebase"

# Interactive mode
delgato interactive
```

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              DELGATO ECOSYSTEM                               │
├─────────────────────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐              │
│  │   Blazor Web    │  │      CLI        │  │   REST/gRPC     │  Interfaces  │
│  │   Dashboard     │  │   Application   │  │      APIs       │              │
│  └────────┬────────┘  └────────┬────────┘  └────────┬────────┘              │
├───────────┴────────────────────┴────────────────────┴───────────────────────┤
│                        ORCHESTRATION LAYER                                   │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌─────────────┐      │
│  │ Tree         │  │ Agent Tree   │  │ Plan         │  │ Capability  │      │
│  │ Orchestrator │  │ Manager      │  │ Executor     │  │ Router      │      │
│  └──────────────┘  └──────────────┘  └──────────────┘  └─────────────┘      │
├─────────────────────────────────────────────────────────────────────────────┤
│                          PROVIDER LAYER                                      │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐      │
│  │ Claude   │  │ OpenAI   │  │ Gemini   │  │ Ollama   │  │ Custom   │      │
│  │ Provider │  │ Provider │  │ Provider │  │ Provider │  │ Provider │      │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘  └──────────┘      │
├─────────────────────────────────────────────────────────────────────────────┤
│                      ENTERPRISE SERVICES                                     │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐      │
│  │ Audit    │  │ Cost     │  │ Compliance│  │ Health   │  │ Telemetry│      │
│  │ Service  │  │ Tracker  │  │ Engine   │  │ Monitor  │  │ Service  │      │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘  └──────────┘      │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Project Structure

```
Delgato/
├── Delgato.Core/              # Domain models, abstractions, governance interfaces
├── Delgato.Providers/         # LLM provider implementations (Claude, OpenAI, Gemini)
├── Delgato.Orchestration/     # N-tree orchestrator, planner, router
├── Delgato.Governance/        # Audit service, cost tracker, compliance
├── Delgato.Cli/               # Full-featured command-line interface
├── Delgato.Dashboard.Web/     # Blazor web dashboard
├── Delgato.Configuration/     # YAML/JSON DSL parsing, fluent builders
├── Delgato.Agents/            # Agent implementations, registry, factory
├── Delgato.Transports/        # Transport adapters (HTTP, etc.)
├── Delgato.Tests/             # TinyBDD behavior tests
├── docs/                      # DocFx documentation
└── agents/                    # Agent definition files (YAML)
```

## Multi-Provider Support

### Supported Providers

| Provider | Models | Features |
|----------|--------|----------|
| **Anthropic** | Claude 3.5 Sonnet, Claude 3 Opus, Haiku | Streaming, Tools, Vision |
| **OpenAI** | GPT-4o, GPT-4 Turbo, o1, o3 | Streaming, Functions, Vision |
| **Google** | Gemini 2.0, Gemini 1.5 Pro/Flash | Streaming, Tools, Grounding |
| **Ollama** | CodeLlama, Mistral, Llama 3 | Local deployment |

### Provider Configuration

```yaml
# agents/my-agent.yaml
model:
  provider: anthropic
  modelId: claude-3-5-sonnet-20241022
  temperature: 0.7
  maxTokens: 4000
```

## N-Tree Orchestration

Agents can spawn child agents in a hierarchical tree structure:

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
     │  Detail1  │   │  Detail2  │
     └───────────┘   └───────────┘
```

### Tree Configuration

```csharp
var options = new TreeOrchestratorOptions
{
    MaxTreeDepth = 10,
    MaxTreeNodes = 100,
    EnableParallelExecution = true,
    MaxParallelTasks = 5
};
```

## Enterprise Governance

### Audit Logging

All operations are tracked with correlation IDs:

```csharp
var events = await auditService.GetByCorrelationIdAsync(correlationId);
var stats = await auditService.GetStatisticsAsync(from, to);
```

### Cost Tracking

Real-time cost monitoring with budget enforcement:

```csharp
await costTracker.SetBudgetLimitAsync(
    new BudgetScope { Type = BudgetScopeType.Project, ProjectId = "proj-1" },
    new BudgetLimit { Limit = 100m, Currency = "USD", Period = BudgetPeriod.Monthly });

var result = await costTracker.CheckBudgetAsync(scope);
// result.IsWithinBudget, result.CurrentSpend, result.RemainingBudget
```

### Compliance

Policy-based compliance validation:

```yaml
policy:
  deniedTools:
    - delete_file
    - execute_shell
  requiredCapabilities:
    - safe-execution
  auditAllToolCalls: true
```

## CLI Reference

```bash
# Agent Management
delgato agent list                    # List all agents
delgato agent show <id>               # Show agent details
delgato agent run <id> "prompt"       # Run an agent
delgato agent create --name "Agent"   # Create agent

# Swarm Operations
delgato swarm status                  # Show swarm status
delgato swarm trees                   # List execution trees
delgato swarm execute "request"       # Execute in swarm

# Provider Management
delgato provider list                 # List providers
delgato provider health               # Check health
delgato provider models <id>          # List models

# Governance
delgato audit search                  # Search audit logs
delgato audit stats                   # View statistics
delgato audit export audit.json       # Export logs

# Health & Config
delgato health check                  # Run health checks
delgato health monitor                # Continuous monitoring
delgato config init                   # Interactive setup
```

## Dashboard Features

The Blazor web dashboard provides:

- **Agents Page**: Full CRUD for agent management with search
- **Swarm Page**: Execution tree visualization and request execution
- **Providers Page**: Provider health monitoring and model listing
- **Audit Page**: Searchable audit log with statistics and export
- **Real-time Updates**: Live status and metrics

## Agent Definition

### YAML Configuration

```yaml
version: "1.0"
agents:
  - id: code-analyzer
    name: Code Analyzer
    description: Analyzes code for quality and patterns

    model:
      provider: anthropic
      modelId: claude-3-5-sonnet-20241022
      temperature: 0.3

    capabilities:
      - code-analysis
      - pattern-detection

    budget:
      maxTokens: 100000
      maxToolCalls: 100
      maxDepth: 3
      maxCost: 2.0

    policy:
      deniedTools:
        - delete_file
      auditAllToolCalls: true
```

### Fluent API

```csharp
var agent = new AgentDefinitionBuilder()
    .WithId("code-analyzer")
    .WithName("Code Analyzer")
    .WithModel("anthropic", "claude-3-5-sonnet-20241022")
    .WithCapabilities("code-analysis", "pattern-detection")
    .WithBudget(b => b
        .WithMaxTokens(100000)
        .WithMaxCost(2.0m))
    .Build();
```

## Testing

### TinyBDD Behavior Tests

```csharp
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
}
```

### Run Tests

```bash
dotnet test
dotnet test --filter "Category=Integration"
dotnet test --collect:"XPlat Code Coverage"
```

## Documentation

Full documentation is available at [docs/](docs/):

- [Getting Started](docs/GETTING_STARTED.md)
- [Architecture](docs/docfx/articles/architecture.md)
- [CLI Reference](docs/docfx/articles/cli-reference.md)
- [API Reference](docs/docfx/api/)

### Build Documentation

```bash
cd docs/docfx
dotnet tool install -g docfx
docfx build
```

## Environment Variables

| Variable | Description |
|----------|-------------|
| `ANTHROPIC_API_KEY` | Anthropic Claude API key |
| `OPENAI_API_KEY` | OpenAI API key |
| `GOOGLE_API_KEY` | Google Gemini API key |
| `OLLAMA_ENDPOINT` | Ollama server endpoint |
| `DELGATO_AGENTS_PATH` | Path to agents directory |

## Contributing

1. Fork the repository
2. Create a feature branch
3. Write tests (TinyBDD style)
4. Implement your feature
5. Submit a pull request

## License

MIT License - See [LICENSE](LICENSE) file

## Support

- [Documentation](docs/)
- [GitHub Issues](https://github.com/your-org/delgato/issues)
- [Discussions](https://github.com/your-org/delgato/discussions)
