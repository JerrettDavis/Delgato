# Getting Started with Delgato

This guide will help you get up and running with Delgato in minutes.

## Prerequisites

- .NET 10.0 SDK or later
- One or more API keys for LLM providers (OpenAI, Anthropic, Google)
- Git (for cloning the repository)

## Installation

### Option 1: Clone and Build

```bash
# Clone the repository
git clone https://github.com/JerrettDavis/Delgato.git
cd Delgato

# Build the solution
dotnet build

# Run tests
dotnet test
```

### Option 2: Install CLI Tool

```bash
# Install as a global tool
dotnet tool install -g delgato

# Verify installation
delgato --version
```

### Option 3: Docker

```bash
# Pull the image
docker pull delgato/delgato:latest

# Run the dashboard
docker run -p 8080:8080 delgato/delgato:latest
```

## Configuration

### Environment Variables

Set up your provider API keys:

```bash
# Anthropic Claude
export ANTHROPIC_API_KEY=your-anthropic-key

# OpenAI
export OPENAI_API_KEY=your-openai-key

# Google Gemini
export GOOGLE_API_KEY=your-google-key
```

### Configuration File

Create a configuration file at `~/.delgato/config.json`:

```json
{
  "agents_path": "./agents",
  "default_provider": "anthropic",
  "default_model": "claude-3-5-sonnet-20241022",
  "max_tokens": 4000,
  "max_depth": 5
}
```

Or use the interactive setup:

```bash
delgato config init
```

## Your First Agent

### 1. Create an Agent Definition

Create a file `agents/my-assistant.yaml`:

```yaml
version: "1.0"
agents:
  - id: my-assistant
    name: My Assistant
    description: A helpful AI assistant
    model:
      provider: anthropic
      modelId: claude-3-5-sonnet-20241022
      temperature: 0.7
    capabilities:
      - general-assistance
      - reasoning
    budget:
      maxTokens: 10000
      maxToolCalls: 50
```

### 2. Run the Agent

Using the CLI:

```bash
# List agents to verify it's loaded
delgato agent list

# Run the agent
delgato agent run my-assistant "Hello! What can you help me with?"
```

Using the Dashboard:

1. Navigate to `http://localhost:5000`
2. Click on "Agents" in the navigation
3. Select "my-assistant"
4. Enter your prompt and click "Execute"

## Running the Dashboard

```bash
# Start the web dashboard
dotnet run --project Delgato.Dashboard.Web

# Or with Aspire for full orchestration
dotnet run --project Delgato.Dashboard.AppHost
```

The dashboard will be available at `http://localhost:5000`.

## Interactive Mode

Start an interactive session with the CLI:

```bash
delgato interactive

# Commands in interactive mode:
# > agents          - List available agents
# > use my-assistant - Select an agent
# > clear           - Clear conversation
# > exit            - Exit interactive mode
```

## Next Steps

- [Learn about the architecture](architecture.md)
- [Create custom agents](creating-agents.md)
- [Configure providers](providers.md)
- [Set up governance](governance.md)
- [Deploy to production](production-checklist.md)

## Troubleshooting

### Agent Not Found

Ensure your agents directory is correctly configured:

```bash
delgato config show
```

### API Key Issues

Verify your API keys are set:

```bash
# Check if keys are configured
delgato provider health
```

### Health Check

Run a full system health check:

```bash
delgato health check
```

## Getting Help

- Check the [FAQ](faq.md)
- Search [GitHub Issues](https://github.com/JerrettDavis/Delgato/issues)
- Join our [Discord Community](https://discord.gg/delgato)
