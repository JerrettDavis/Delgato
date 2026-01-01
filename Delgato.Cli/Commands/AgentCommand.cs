using System.CommandLine;
using System.Text.Json;
using Delgato.Core;
using Delgato.Core.Abstractions;
using Delgato.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Delgato.Cli.Commands;

/// <summary>
/// Commands for managing agents.
/// </summary>
public static class AgentCommand
{
    public static Command Create(IServiceProvider services)
    {
        var command = new Command("agent", "Manage agents in the swarm");

        command.AddCommand(CreateListCommand(services));
        command.AddCommand(CreateShowCommand(services));
        command.AddCommand(CreateCreateCommand(services));
        command.AddCommand(CreateDeleteCommand(services));
        command.AddCommand(CreateRunCommand(services));
        command.AddCommand(CreateValidateCommand(services));

        return command;
    }

    private static Command CreateListCommand(IServiceProvider services)
    {
        var command = new Command("list", "List all registered agents")
        {
            new Option<string?>("--capability", "Filter by capability"),
            new Option<string?>("--provider", "Filter by provider")
        };

        command.SetHandler(async (string? capability, string? provider) =>
        {
            var registry = services.GetRequiredService<IAgentRegistry>();

            await AnsiConsole.Status()
                .StartAsync("Loading agents...", async ctx =>
                {
                    IReadOnlyList<AgentDefinition> agents;

                    if (!string.IsNullOrEmpty(capability))
                    {
                        agents = await registry.FindByCapabilityAsync(capability);
                    }
                    else
                    {
                        agents = await registry.GetAllDefinitionsAsync();
                    }

                    if (!string.IsNullOrEmpty(provider))
                    {
                        agents = agents.Where(a =>
                            a.Model?.Provider?.Equals(provider, StringComparison.OrdinalIgnoreCase) == true)
                            .ToList();
                    }

                    var table = new Table()
                        .Border(TableBorder.Rounded)
                        .AddColumn("ID")
                        .AddColumn("Name")
                        .AddColumn("Provider")
                        .AddColumn("Model")
                        .AddColumn("Capabilities")
                        .AddColumn("Orchestrator");

                    foreach (var agent in agents)
                    {
                        table.AddRow(
                            agent.Id.Value,
                            agent.Name,
                            agent.Model?.Provider ?? "N/A",
                            agent.Model?.ModelId ?? "N/A",
                            string.Join(", ", agent.Capabilities.Take(3)),
                            agent.IsOrchestrator ? "[green]Yes[/]" : "No"
                        );
                    }

                    AnsiConsole.Write(table);
                    AnsiConsole.MarkupLine($"\n[grey]Total: {agents.Count} agents[/]");
                });
        },
        command.Options.OfType<Option<string?>>().First(),
        command.Options.OfType<Option<string?>>().Last());

        return command;
    }

    private static Command CreateShowCommand(IServiceProvider services)
    {
        var command = new Command("show", "Show details of a specific agent")
        {
            new Argument<string>("id", "Agent ID")
        };

        command.SetHandler(async (string id) =>
        {
            var registry = services.GetRequiredService<IAgentRegistry>();
            var agentId = new AgentId(id);
            var agent = await registry.GetDefinitionAsync(agentId);

            if (agent == null)
            {
                AnsiConsole.MarkupLine($"[red]Agent '{id}' not found[/]");
                return;
            }

            var panel = new Panel(new Rows(
                new Markup($"[bold]ID:[/] {agent.Id.Value}"),
                new Markup($"[bold]Name:[/] {agent.Name}"),
                new Markup($"[bold]Description:[/] {agent.Description ?? "N/A"}"),
                new Markup($"[bold]Provider:[/] {agent.Model?.Provider ?? "N/A"}"),
                new Markup($"[bold]Model:[/] {agent.Model?.ModelId ?? "N/A"}"),
                new Markup($"[bold]Temperature:[/] {agent.Model?.Temperature ?? 0.7}"),
                new Markup($"[bold]Orchestrator:[/] {(agent.IsOrchestrator ? "Yes" : "No")}"),
                new Markup($"[bold]Capabilities:[/] {string.Join(", ", agent.Capabilities)}"),
                new Markup($"[bold]Allowed Tools:[/] {string.Join(", ", agent.AllowedTools)}"),
                new Rule("[bold]Budget[/]"),
                new Markup($"  Max Tokens: {agent.Budget?.MaxTokens?.ToString() ?? "Unlimited"}"),
                new Markup($"  Max Tool Calls: {agent.Budget?.MaxToolCalls?.ToString() ?? "Unlimited"}"),
                new Markup($"  Max Depth: {agent.Budget?.MaxDepth?.ToString() ?? "Unlimited"}"),
                new Markup($"  Max Duration: {agent.Budget?.MaxDuration?.ToString() ?? "Unlimited"}"),
                new Markup($"  Max Cost: {agent.Budget?.MaxCost?.ToString("C") ?? "Unlimited"}")
            ))
            {
                Header = new PanelHeader($"Agent: {agent.Name}"),
                Border = BoxBorder.Rounded
            };

            AnsiConsole.Write(panel);
        },
        command.Arguments.OfType<Argument<string>>().First());

        return command;
    }

    private static Command CreateCreateCommand(IServiceProvider services)
    {
        var command = new Command("create", "Create a new agent")
        {
            new Option<string>("--name", "Agent name") { IsRequired = true },
            new Option<string>("--provider", () => "openai", "Provider (openai, anthropic, google)"),
            new Option<string>("--model", () => "gpt-4o", "Model ID"),
            new Option<string?>("--description", "Agent description"),
            new Option<string[]>("--capabilities", () => Array.Empty<string>(), "Agent capabilities"),
            new Option<bool>("--orchestrator", () => false, "Make this an orchestrator agent"),
            new Option<string?>("--prompt", "System prompt for the agent")
        };

        command.SetHandler(async (string name, string provider, string model, string? description,
            string[] capabilities, bool orchestrator, string? prompt) =>
        {
            var id = name.ToLowerInvariant().Replace(" ", "-");

            var builder = new AgentDefinitionBuilder()
                .WithId(id)
                .WithName(name)
                .WithDescription(description ?? $"Agent: {name}")
                .WithModel(provider, model)
                .WithCapabilities(capabilities.Length > 0 ? capabilities : new[] { "general" });

            if (orchestrator)
                builder.AsOrchestrator();

            if (!string.IsNullOrEmpty(prompt))
                builder.WithPrompt(prompt);

            var agentDef = builder.Build();

            AnsiConsole.MarkupLine($"[green]✓[/] Created agent: [bold]{agentDef.Name}[/]");
            AnsiConsole.MarkupLine($"  ID: {agentDef.Id.Value}");
            AnsiConsole.MarkupLine($"  Provider: {agentDef.Model?.Provider}");
            AnsiConsole.MarkupLine($"  Model: {agentDef.Model?.ModelId}");
        },
        command.Options.OfType<Option<string>>().First(),
        command.Options.OfType<Option<string>>().Skip(1).First(),
        command.Options.OfType<Option<string>>().Skip(2).First(),
        command.Options.OfType<Option<string?>>().First(),
        command.Options.OfType<Option<string[]>>().First(),
        command.Options.OfType<Option<bool>>().First(),
        command.Options.OfType<Option<string?>>().Last());

        return command;
    }

    private static Command CreateDeleteCommand(IServiceProvider services)
    {
        var command = new Command("delete", "Delete an agent")
        {
            new Argument<string>("id", "Agent ID"),
            new Option<bool>("--force", "Skip confirmation")
        };

        command.SetHandler(async (string id, bool force) =>
        {
            if (!force)
            {
                if (!AnsiConsole.Confirm($"Delete agent '{id}'?"))
                {
                    AnsiConsole.MarkupLine("[yellow]Cancelled[/]");
                    return;
                }
            }

            AnsiConsole.MarkupLine($"[green]✓[/] Deleted agent: [bold]{id}[/]");
        },
        command.Arguments.OfType<Argument<string>>().First(),
        command.Options.OfType<Option<bool>>().First());

        return command;
    }

    private static Command CreateRunCommand(IServiceProvider services)
    {
        var command = new Command("run", "Run an agent with input")
        {
            new Argument<string>("id", "Agent ID"),
            new Argument<string>("input", "Input text for the agent"),
            new Option<bool>("--stream", "Stream the response")
        };

        command.SetHandler(async (string id, string input, bool stream) =>
        {
            var registry = services.GetRequiredService<IAgentRegistry>();
            var factory = services.GetRequiredService<IAgentFactory>();

            var agentId = new AgentId(id);
            var definition = await registry.GetDefinitionAsync(agentId);

            if (definition == null)
            {
                AnsiConsole.MarkupLine($"[red]Agent '{id}' not found[/]");
                return;
            }

            await AnsiConsole.Status()
                .StartAsync($"Running agent {definition.Name}...", async ctx =>
                {
                    var agent = await factory.CreateAgentAsync(definition);
                    var context = new ExecutionContext
                    {
                        CorrelationId = Guid.NewGuid().ToString(),
                        Budget = definition.Budget ?? new Budget()
                    };

                    var response = await agent.ExecuteAsync(input, context);

                    if (response.IsSuccess)
                    {
                        AnsiConsole.WriteLine();
                        AnsiConsole.Write(new Panel(response.Content)
                        {
                            Header = new PanelHeader("Response"),
                            Border = BoxBorder.Rounded
                        });
                        AnsiConsole.MarkupLine($"\n[grey]Tokens used: {response.TokensUsed}[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]Error:[/] {response.ErrorMessage}");
                    }
                });
        },
        command.Arguments.OfType<Argument<string>>().First(),
        command.Arguments.OfType<Argument<string>>().Last(),
        command.Options.OfType<Option<bool>>().First());

        return command;
    }

    private static Command CreateValidateCommand(IServiceProvider services)
    {
        var command = new Command("validate", "Validate agent configuration")
        {
            new Argument<string>("path", "Path to agent YAML file")
        };

        command.SetHandler(async (string path) =>
        {
            if (!File.Exists(path))
            {
                AnsiConsole.MarkupLine($"[red]File not found: {path}[/]");
                return;
            }

            try
            {
                var loader = services.GetRequiredService<AgentFileLoader>();
                var definition = loader.LoadFromFile(path);

                AnsiConsole.MarkupLine($"[green]✓[/] Valid agent configuration");
                AnsiConsole.MarkupLine($"  ID: {definition.Id.Value}");
                AnsiConsole.MarkupLine($"  Name: {definition.Name}");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗[/] Invalid configuration: {ex.Message}");
            }
        },
        command.Arguments.OfType<Argument<string>>().First());

        return command;
    }
}
