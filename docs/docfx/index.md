# Delgato Documentation

Welcome to the official documentation for **Delgato** - the Enterprise Agent Swarm Ecosystem.

## What is Delgato?

Delgato is an extensible, pluggable, secure, enterprise-grade agent swarm platform designed for production deployment. It enables organizations to orchestrate multiple AI agents across different providers (Claude, OpenAI, Gemini, and more) in a hierarchical N-tree structure.

## Key Features

- **Multi-Provider Support**: Seamlessly integrate with Claude, OpenAI, Gemini, Ollama, and custom providers
- **N-Tree Orchestration**: Hierarchical agent spawning with unlimited depth for complex task decomposition
- **Enterprise Governance**: Full audit trails, compliance tracking, cost management, and security controls
- **Production-Ready**: DRY, SOLID, modular architecture with comprehensive testing and documentation
- **Dual Interface**: Both CLI and web dashboard for complete control

## Quick Start

### Installation

```bash
# Clone the repository
git clone https://github.com/JerrettDavis/Delgato.git

# Build the solution
dotnet build

# Run the dashboard
dotnet run --project Delgato.Dashboard.Web
```

### CLI Usage

```bash
# List all agents
delgato agent list

# Run an agent
delgato agent run orchestrator "Analyze this codebase"

# Execute a swarm request
delgato swarm execute "Build a REST API for user management"

# Check system health
delgato health check
```

## Documentation Sections

### [Getting Started](articles/getting-started.md)
Learn how to install, configure, and run Delgato.

### [Architecture](articles/architecture.md)
Understand the system architecture and design patterns.

### [Agents](articles/agents.md)
Learn how to create, configure, and manage agents.

### [Orchestration](articles/orchestration.md)
Understand the N-tree orchestration model.

### [Providers](articles/providers.md)
Configure and use different LLM providers.

### [Governance](articles/governance.md)
Set up auditing, compliance, and cost tracking.

### [CLI Reference](articles/cli-reference.md)
Complete command-line interface documentation.

### [API Reference](api/index.md)
Full API documentation for developers.

## Support

- [GitHub Issues](https://github.com/JerrettDavis/Delgato/issues)
- [Documentation](https://delgato.dev/docs)
- [Community Discord](https://discord.gg/delgato)
