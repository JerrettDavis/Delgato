# CLI Reference

Complete command reference for the Delgato CLI.

## Installation

```bash
# Install as a global tool
dotnet tool install -g delgato

# Verify installation
delgato --version
```

## Global Options

| Option | Alias | Description |
|--------|-------|-------------|
| `--verbose` | `-v` | Enable verbose output |
| `--output` | `-o` | Output format (table, json, yaml) |
| `--config` | `-c` | Path to configuration file |
| `--help` | `-h` | Show help information |

## Commands

### agent

Manage agents in the swarm.

#### agent list

List all registered agents.

```bash
delgato agent list
delgato agent list --capability code-generation
delgato agent list --provider anthropic
```

| Option | Description |
|--------|-------------|
| `--capability` | Filter by capability |
| `--provider` | Filter by provider |

#### agent show

Show details of a specific agent.

```bash
delgato agent show <agent-id>
```

#### agent create

Create a new agent.

```bash
delgato agent create --name "My Agent" --provider openai --model gpt-4o
```

| Option | Description |
|--------|-------------|
| `--name` | Agent name (required) |
| `--provider` | Provider (openai, anthropic, google) |
| `--model` | Model ID |
| `--description` | Agent description |
| `--capabilities` | Agent capabilities |
| `--orchestrator` | Make this an orchestrator |
| `--prompt` | System prompt |

#### agent delete

Delete an agent.

```bash
delgato agent delete <agent-id>
delgato agent delete <agent-id> --force
```

#### agent run

Run an agent with input.

```bash
delgato agent run <agent-id> "Your prompt here"
delgato agent run orchestrator "Analyze this code" --stream
```

| Option | Description |
|--------|-------------|
| `--stream` | Stream the response |

#### agent validate

Validate an agent configuration file.

```bash
delgato agent validate path/to/agent.yaml
```

---

### swarm

Manage the agent swarm.

#### swarm status

Show swarm status.

```bash
delgato swarm status
```

#### swarm trees

Show active execution trees.

```bash
delgato swarm trees
```

#### swarm execute

Execute a request in the swarm.

```bash
delgato swarm execute "Build a REST API"
delgato swarm execute "Analyze this" --max-depth 3 --max-tokens 5000
```

| Option | Description |
|--------|-------------|
| `--max-depth` | Maximum tree depth (default: 5) |
| `--max-tokens` | Maximum tokens (default: 10000) |
| `--max-cost` | Maximum cost in USD (default: 1.0) |

---

### provider

Manage LLM providers.

#### provider list

List all registered providers.

```bash
delgato provider list
```

#### provider health

Check health of all providers.

```bash
delgato provider health
```

#### provider models

List models for a provider.

```bash
delgato provider models anthropic
```

---

### config

Manage configuration.

#### config show

Show current configuration.

```bash
delgato config show
```

#### config set

Set a configuration value.

```bash
delgato config set default_provider anthropic
delgato config set max_tokens 8000
```

#### config list

List available configuration keys.

```bash
delgato config list
```

#### config init

Initialize configuration interactively.

```bash
delgato config init
```

---

### audit

Manage audit logs.

#### audit search

Search audit events.

```bash
delgato audit search
delgato audit search --type AgentExecution --limit 50
delgato audit search --actor user-123 --outcome Success
```

| Option | Description |
|--------|-------------|
| `--type` | Filter by event type |
| `--actor` | Filter by actor ID |
| `--resource` | Filter by resource type |
| `--correlation` | Filter by correlation ID |
| `--limit` | Maximum results (default: 20) |

#### audit stats

Show audit statistics.

```bash
delgato audit stats
delgato audit stats --days 30
```

#### audit export

Export audit logs.

```bash
delgato audit export audit.json
delgato audit export audit.csv --format csv --days 7
```

---

### health

Health monitoring commands.

#### health check

Run health checks.

```bash
delgato health check
```

#### health monitor

Monitor system health continuously.

```bash
delgato health monitor
delgato health monitor --interval 60
```

---

### run

Quick run command for executing requests.

```bash
delgato run "Your request here"
delgato run "Analyze code" --agent code-analyzer
delgato run "Build feature" --json --max-tokens 5000
```

| Option | Description |
|--------|-------------|
| `--agent` | Specific agent to use |
| `--stream` | Stream the response |
| `--max-tokens` | Maximum tokens |
| `--json` | Output as JSON |

---

### interactive

Start interactive REPL mode.

```bash
delgato interactive
delgato interactive --agent my-assistant
```

**Interactive Commands:**

| Command | Description |
|---------|-------------|
| `agents` | List available agents |
| `use <id>` | Select an agent |
| `clear` | Clear conversation |
| `history` | Show conversation history |
| `status` | Show system status |
| `help` | Show help |
| `exit` | Exit interactive mode |

## Examples

### Basic Usage

```bash
# List all agents
delgato agent list

# Run an agent
delgato agent run my-assistant "What is the weather today?"

# Execute in the swarm
delgato swarm execute "Refactor this codebase for better performance"
```

### JSON Output for Scripting

```bash
# Get agent list as JSON
delgato agent list -o json

# Execute and get JSON response
delgato run "Analyze this" --json
```

### Interactive Session

```bash
delgato interactive

# In interactive mode:
> agents
> use orchestrator
> Hello, what can you help me with?
> clear
> exit
```

### Health Monitoring

```bash
# One-time health check
delgato health check

# Continuous monitoring
delgato health monitor --interval 30
```

### Audit Trail

```bash
# View recent audit events
delgato audit search --limit 10

# Export last 7 days
delgato audit export audit-weekly.json --days 7

# Get statistics
delgato audit stats --days 30
```

## Environment Variables

| Variable | Description |
|----------|-------------|
| `ANTHROPIC_API_KEY` | Anthropic API key |
| `OPENAI_API_KEY` | OpenAI API key |
| `GOOGLE_API_KEY` | Google API key |
| `DELGATO_AGENTS_PATH` | Path to agents directory |
| `DELGATO_DEFAULT_PROVIDER` | Default provider |
| `DELGATO_DEFAULT_MODEL` | Default model ID |
| `DELGATO_LOG_LEVEL` | Logging level |

## Exit Codes

| Code | Description |
|------|-------------|
| 0 | Success |
| 1 | General error |
| 2 | Configuration error |
| 3 | Provider error |
| 4 | Agent not found |
| 5 | Budget exceeded |
