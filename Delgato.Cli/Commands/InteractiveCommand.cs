using System.CommandLine;
using Delgato.Core;
using Delgato.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Delgato.Cli.Commands;

/// <summary>
/// Interactive REPL mode for the CLI.
/// </summary>
public static class InteractiveCommand
{
    public static Command Create(IServiceProvider services)
    {
        var command = new Command("interactive", "Start interactive REPL mode")
        {
            new Option<string?>("--agent", "Default agent to use"),
            new Option<string?>("--provider", "Default provider to use")
        };

        command.SetHandler(async (string? agent, string? provider) =>
        {
            AnsiConsole.MarkupLine("[bold blue]Delgato Interactive Mode[/]");
            AnsiConsole.MarkupLine("[grey]Type 'help' for commands, 'exit' to quit[/]\n");

            var registry = services.GetRequiredService<IAgentRegistry>();
            var factory = services.GetRequiredService<IAgentFactory>();
            var orchestrator = services.GetRequiredService<IOrchestrator>();

            AgentDefinition? currentAgent = null;

            // Set initial agent if specified
            if (!string.IsNullOrEmpty(agent))
            {
                currentAgent = await registry.GetDefinitionAsync(new AgentId(agent));
                if (currentAgent != null)
                {
                    AnsiConsole.MarkupLine($"[green]Using agent:[/] {currentAgent.Name}");
                }
            }

            var conversationHistory = new List<(string Role, string Content)>();

            while (true)
            {
                var prompt = currentAgent != null
                    ? $"[blue]{currentAgent.Name}[/]> "
                    : "[grey]delgato[/]> ";

                var input = AnsiConsole.Prompt(
                    new TextPrompt<string>(prompt)
                        .AllowEmpty());

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                var parts = input.Trim().Split(' ', 2);
                var cmd = parts[0].ToLowerInvariant();
                var arg = parts.Length > 1 ? parts[1] : null;

                switch (cmd)
                {
                    case "exit":
                    case "quit":
                    case "q":
                        AnsiConsole.MarkupLine("[grey]Goodbye![/]");
                        return;

                    case "help":
                    case "?":
                        ShowHelp();
                        break;

                    case "agents":
                        await ListAgents(registry);
                        break;

                    case "use":
                        if (string.IsNullOrEmpty(arg))
                        {
                            AnsiConsole.MarkupLine("[yellow]Usage: use <agent-id>[/]");
                        }
                        else
                        {
                            currentAgent = await registry.GetDefinitionAsync(new AgentId(arg));
                            if (currentAgent != null)
                            {
                                AnsiConsole.MarkupLine($"[green]Now using:[/] {currentAgent.Name}");
                                conversationHistory.Clear();
                            }
                            else
                            {
                                AnsiConsole.MarkupLine($"[red]Agent '{arg}' not found[/]");
                            }
                        }
                        break;

                    case "clear":
                        conversationHistory.Clear();
                        AnsiConsole.Clear();
                        AnsiConsole.MarkupLine("[grey]Conversation cleared[/]");
                        break;

                    case "history":
                        if (conversationHistory.Count == 0)
                        {
                            AnsiConsole.MarkupLine("[grey]No conversation history[/]");
                        }
                        else
                        {
                            foreach (var (role, content) in conversationHistory)
                            {
                                var color = role == "user" ? "blue" : "green";
                                AnsiConsole.MarkupLine($"[{color}]{role}:[/] {Markup.Escape(content[..Math.Min(100, content.Length)])}...");
                            }
                        }
                        break;

                    case "status":
                        await ShowStatus(registry, services.GetRequiredService<IProviderRegistry>());
                        break;

                    default:
                        // Treat as input to current agent
                        if (currentAgent == null)
                        {
                            AnsiConsole.MarkupLine("[yellow]No agent selected. Use 'use <agent-id>' or ask the orchestrator.[/]");

                            // Route to orchestrator
                            var envelope = new RequestEnvelope
                            {
                                CorrelationId = Guid.NewGuid().ToString(),
                                Source = "cli-interactive",
                                Payload = input,
                                Timestamp = DateTimeOffset.UtcNow
                            };

                            var context = new ExecutionContext
                            {
                                CorrelationId = envelope.CorrelationId,
                                Budget = new Budget { MaxTokens = 4000 }
                            };

                            await AnsiConsole.Status()
                                .StartAsync("Processing...", async ctx =>
                                {
                                    var plan = await orchestrator.CreatePlanAsync(envelope, context);
                                    var result = await orchestrator.ExecutePlanAsync(plan, context);

                                    foreach (var task in result.Tasks.Where(t => !string.IsNullOrEmpty(t.Result)))
                                    {
                                        AnsiConsole.WriteLine();
                                        AnsiConsole.Write(new Panel(task.Result!)
                                        {
                                            Border = BoxBorder.Rounded
                                        });
                                    }
                                });
                        }
                        else
                        {
                            conversationHistory.Add(("user", input));

                            await AnsiConsole.Status()
                                .StartAsync("Thinking...", async ctx =>
                                {
                                    var agentInstance = await factory.CreateAgentAsync(currentAgent);
                                    var context = new ExecutionContext
                                    {
                                        CorrelationId = Guid.NewGuid().ToString(),
                                        Budget = currentAgent.Budget ?? new Budget { MaxTokens = 4000 }
                                    };

                                    var response = await agentInstance.ExecuteAsync(input, context);

                                    if (response.IsSuccess)
                                    {
                                        conversationHistory.Add(("assistant", response.Content));

                                        AnsiConsole.WriteLine();
                                        AnsiConsole.Write(new Panel(response.Content)
                                        {
                                            Border = BoxBorder.Rounded,
                                            Padding = new Padding(1, 0, 1, 0)
                                        });
                                        AnsiConsole.MarkupLine($"[grey]({response.TokensUsed} tokens)[/]");
                                    }
                                    else
                                    {
                                        AnsiConsole.MarkupLine($"[red]Error:[/] {response.ErrorMessage}");
                                    }
                                });
                        }
                        break;
                }

                AnsiConsole.WriteLine();
            }
        },
        command.Options.OfType<Option<string?>>().First(),
        command.Options.OfType<Option<string?>>().Last());

        return command;
    }

    private static void ShowHelp()
    {
        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn("Command")
            .AddColumn("Description");

        table.AddRow("[blue]agents[/]", "List available agents");
        table.AddRow("[blue]use <id>[/]", "Select an agent to chat with");
        table.AddRow("[blue]clear[/]", "Clear conversation history");
        table.AddRow("[blue]history[/]", "Show conversation history");
        table.AddRow("[blue]status[/]", "Show system status");
        table.AddRow("[blue]help[/]", "Show this help");
        table.AddRow("[blue]exit[/]", "Exit interactive mode");
        table.AddRow("", "");
        table.AddRow("[grey]Any other input[/]", "Send to current agent or orchestrator");

        AnsiConsole.Write(table);
    }

    private static async Task ListAgents(IAgentRegistry registry)
    {
        var agents = await registry.GetAllDefinitionsAsync();

        var table = new Table()
            .Border(TableBorder.Simple)
            .AddColumn("ID")
            .AddColumn("Name")
            .AddColumn("Provider");

        foreach (var agent in agents)
        {
            table.AddRow(
                agent.Id.Value,
                agent.Name,
                agent.Model?.Provider ?? "N/A"
            );
        }

        AnsiConsole.Write(table);
    }

    private static async Task ShowStatus(IAgentRegistry registry, IProviderRegistry providerRegistry)
    {
        var agents = await registry.GetAllDefinitionsAsync();
        var providers = providerRegistry.GetAll();
        var health = await providerRegistry.CheckHealthAsync();

        AnsiConsole.MarkupLine($"[bold]Agents:[/] {agents.Count}");
        AnsiConsole.MarkupLine($"[bold]Providers:[/] {providers.Count} ({health.Count(h => h.Value.IsHealthy)} healthy)");
    }
}
