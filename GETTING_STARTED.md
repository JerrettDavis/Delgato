# Delgato Quick Start Guide

This guide will help you get started with Delgato in 5 minutes.

## Prerequisites

- .NET 10.0 SDK installed
- (Optional) OpenAI API key or Azure OpenAI credentials for production use

## Installation

1. **Clone or navigate to the repository**:
   ```bash
   cd G:\git\Delgato
   ```

2. **Build the solution**:
   ```bash
   dotnet build
   ```

3. **Run tests** (optional):
   ```bash
   dotnet test
   ```

## Configuration

### Agent Directory

Agent definitions are loaded from the `agents/` directory. Three example agents are already configured:

- `orchestrator.yaml` - Master orchestrator
- `repo-analyzer.yaml` - Code analysis specialist
- `mcp-tooling.yaml` - MCP integration specialist

### Environment Variables (Optional)

For production use with real LLM providers:

```bash
# OpenAI
export OPENAI_API_KEY=sk-your-key-here

# OR Azure OpenAI
export AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com
export AZURE_OPENAI_API_KEY=your-key-here
```

## Running Delgato

### Option 1: Basic Mode (No HTTP API)

```bash
cd Delgato
dotnet run
```

This starts the worker service that processes requests from transport adapters.

### Option 2: With HTTP API

```bash
cd Delgato
dotnet run -- --enable-api
```

Or set environment variable:

```bash
export DELGATO_ENABLE_API=true
dotnet run
```

The API will be available at `http://localhost:5000`.

## Using the HTTP API

### Health Check

```bash
curl http://localhost:5000/api/health
```

Response:
```json
{
  "status": "healthy",
  "service": "Delgato Agent Swarm",
  "timestamp": "2025-12-30T22:00:00.000Z"
}
```

### List Available Agents

```bash
curl http://localhost:5000/api/agents
```

Response:
```json
{
  "count": 3,
  "agents": [
    {
      "id": "orchestrator",
      "name": "Master Orchestrator",
      "description": "Primary orchestrator...",
      "capabilities": ["orchestration", "planning", "routing"],
      "isOrchestrator": true,
      ...
    },
    ...
  ]
}
```

### Get Specific Agent

```bash
curl http://localhost:5000/api/agents/orchestrator
```

### Execute Request

```bash
curl -X POST http://localhost:5000/api/execute \
  -H "Content-Type: application/json" \
  -d '{
    "payload": {
      "task": "Analyze the repository structure"
    },
    "userId": "user123"
  }'
```

Response:
```json
{
  "planId": "plan-uuid",
  "status": "Completed",
  "tasks": [
    {
      "taskId": "task-uuid",
      "status": "Completed",
      "result": "...",
      "error": null
    }
  ],
  "tokensUsed": 150,
  "toolCalls": 0,
  "durationMs": 1234
}
```

## Programmatic Usage

### Using the HTTP Transport Adapter

```csharp
using Delgato.Transports.Http;

var transport = serviceProvider.GetRequiredService<HttpTransportAdapter>();

var response = await transport.SubmitRequestAsync(
    payload: new { task = "Analyze code" },
    metadata: new Dictionary<string, string>
    {
        ["userId"] = "user123"
    });

Console.WriteLine($"Response: {response}");
```

### Creating Agents Programmatically

```csharp
using Delgato.Configuration.Builders;

var agent = new AgentDefinitionBuilder()
    .WithId("my-agent")
    .WithName("My Custom Agent")
    .WithPrompt("You are a helpful assistant...")
    .WithModel(m => m
        .WithProvider("openai")
        .WithModelId("gpt-4")
        .WithTemperature(0.7))
    .WithCapabilities("custom-capability")
    .WithBudget(b => b
        .WithMaxTokens(10000)
        .WithMaxDepth(3))
    .Build();

await agentRegistry.RegisterAsync(agent);
```

## Creating Custom Agents

1. **Create a YAML file** in `agents/` directory:

```yaml
version: "1.0"
agents:
  - id: my-agent
    name: My Custom Agent
    description: Does something useful
    prompt: |
      You are a helpful assistant that...
    model:
      provider: openai
      modelId: gpt-4
      temperature: 0.7
    capabilities:
      - custom-capability
    budget:
      maxTokens: 10000
      maxDepth: 3
```

2. **Restart Delgato** - agents are loaded on startup

3. **Verify** the agent is loaded:
```bash
curl http://localhost:5000/api/agents/my-agent
```

## Monitoring & Logs

Logs are written to console with structured JSON format:

```json
{
  "timestamp": "2025-12-30T22:00:00.000Z",
  "level": "Information",
  "category": "Delgato.Worker",
  "message": "Plan {PlanId} completed with status: {Status}",
  "properties": {
    "planId": "plan-123",
    "status": "Completed",
    "correlationId": "corr-456"
  }
}
```

Filter logs by category:
```bash
dotnet run | grep "Delgato.Orchestration"
```

## Troubleshooting

### No agents loaded

**Problem**: `Loaded 0 agent definitions`

**Solution**: 
- Check agent directory path in `appsettings.json`
- Verify YAML files are valid
- Check file permissions

### API key errors

**Problem**: `401 Unauthorized` from OpenAI

**Solution**:
- Set `OPENAI_API_KEY` environment variable
- Or configure `AZURE_OPENAI_*` variables
- For testing, agents will fail gracefully

### Port already in use

**Problem**: `Address already in use: 5000`

**Solution**:
- Change port in `appsettings.json` under `Kestrel.Endpoints.Http.Url`
- Or set `ASPNETCORE_URLS` environment variable:
  ```bash
  export ASPNETCORE_URLS="http://localhost:5001"
  ```

## Next Steps

- Read the full [README.md](README.md) for architecture details
- Review [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) for roadmap
- Check [PROJECT_STATUS.md](PROJECT_STATUS.md) for current progress
- Explore example agents in `agents/` directory
- Write custom agents for your use case

## Examples

### Example 1: Code Analysis

```bash
curl -X POST http://localhost:5000/api/execute \
  -H "Content-Type: application/json" \
  -d '{
    "payload": {
      "task": "Analyze the C# code in Program.cs and suggest improvements"
    }
  }'
```

### Example 2: Multi-Step Task

```bash
curl -X POST http://localhost:5000/api/execute \
  -H "Content-Type: application/json" \
  -d '{
    "payload": {
      "task": "First, analyze the repository structure. Then, identify design patterns. Finally, suggest refactorings."
    }
  }'
```

### Example 3: With Budget Constraints

Create a custom agent with strict budget:

```yaml
budget:
  maxTokens: 1000
  maxToolCalls: 5
  maxDepth: 2
  maxDurationSeconds: 60
```

## Advanced Topics

### Custom Tool Integration

See `IMPLEMENTATION_PLAN.md` section on "Tooling Model & Safety"

### MCP Server Integration

Coming in v1 - see roadmap in `PROJECT_STATUS.md`

### Multi-Agent Coordination

The orchestrator automatically coordinates multiple agents based on capabilities.

### Extending Transports

Implement `ITransportAdapter` to add new ingress methods (MCP, filesystem, etc.)

---

**Need Help?**
- Check logs for detailed error messages
- Review test suite for usage examples
- See architecture documentation in README.md

