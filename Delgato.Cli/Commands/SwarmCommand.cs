using System.CommandLine;
using Delgato.Core;
using Delgato.Core.Abstractions;
using Delgato.Orchestration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Delgato.Cli.Commands;

/// <summary>
/// Commands for managing the agent swarm.
/// </summary>
public static class SwarmCommand
{
    public static Command Create(IServiceProvider services)
    {
        var command = new Command("swarm", "Manage the agent swarm");

        command.AddCommand(CreateStatusCommand(services));
        command.AddCommand(CreateTreesCommand(services));
        command.AddCommand(CreateExecuteCommand(services));

        return command;
    }

    private static Command CreateStatusCommand(IServiceProvider services)
    {
        var command = new Command("status", "Show swarm status");

        command.SetHandler(async () =>
        {
            var registry = services.GetRequiredService<IAgentRegistry>();
            var providerRegistry = services.GetRequiredService<IProviderRegistry>();
            var orchestrator = services.GetRequiredService<IOrchestrator>();

            await AnsiConsole.Status()
                .StartAsync("Checking swarm status...", async ctx =>
                {
                    var agents = await registry.GetAllDefinitionsAsync();
                    var providers = providerRegistry.GetAll();

                    var panel = new Panel(new Rows(
                        new Rule("[bold]Agents[/]"),
                        new Markup($"  Total Agents: [green]{agents.Count}[/]"),
                        new Markup($"  Orchestrators: [blue]{agents.Count(a => a.IsOrchestrator)}[/]"),
                        new Markup($"  Workers: [grey]{agents.Count(a => !a.IsOrchestrator)}[/]"),
                        new Text(""),
                        new Rule("[bold]Providers[/]"),
                        new Markup($"  Registered: [green]{providers.Count}[/]"),
                        new Markup($"  Available: {string.Join(", ", providers.Select(p => p.DisplayName))}")
                    ))
                    {
                        Header = new PanelHeader("Swarm Status"),
                        Border = BoxBorder.Rounded
                    };

                    AnsiConsole.Write(panel);

                    // Check provider health
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[bold]Provider Health:[/]");

                    var healthResults = await providerRegistry.CheckHealthAsync();
                    foreach (var (providerId, result) in healthResults)
                    {
                        var status = result.IsHealthy ? "[green]●[/]" : "[red]●[/]";
                        var latency = result.Latency.HasValue ? $" ({result.Latency.Value.TotalMilliseconds:F0}ms)" : "";
                        AnsiConsole.MarkupLine($"  {status} {providerId}: {result.Status}{latency}");
                    }
                });
        });

        return command;
    }

    private static Command CreateTreesCommand(IServiceProvider services)
    {
        var command = new Command("trees", "Show active execution trees");

        command.SetHandler(() =>
        {
            var orchestrator = services.GetRequiredService<IOrchestrator>();

            if (orchestrator is TreeOrchestrator treeOrch)
            {
                var summaries = treeOrch.GetTreeSummaries();

                if (summaries.Count == 0)
                {
                    AnsiConsole.MarkupLine("[grey]No active execution trees[/]");
                    return;
                }

                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("Tree ID")
                    .AddColumn("Correlation ID")
                    .AddColumn("State")
                    .AddColumn("Nodes")
                    .AddColumn("Depth")
                    .AddColumn("Tokens")
                    .AddColumn("Cost");

                foreach (var summary in summaries)
                {
                    var stateColor = summary.State switch
                    {
                        Core.AgentTree.AgentTreeState.Running => "yellow",
                        Core.AgentTree.AgentTreeState.Completed => "green",
                        Core.AgentTree.AgentTreeState.Failed => "red",
                        _ => "grey"
                    };

                    table.AddRow(
                        summary.TreeId.ToString()[..8],
                        summary.CorrelationId[..Math.Min(20, summary.CorrelationId.Length)],
                        $"[{stateColor}]{summary.State}[/]",
                        summary.NodeCount.ToString(),
                        summary.MaxDepthReached.ToString(),
                        summary.TotalTokensUsed.ToString(),
                        summary.TotalCost.ToString("C")
                    );
                }

                AnsiConsole.Write(table);
            }
            else
            {
                AnsiConsole.MarkupLine("[grey]Tree orchestrator not available[/]");
            }
        });

        return command;
    }

    private static Command CreateExecuteCommand(IServiceProvider services)
    {
        var command = new Command("execute", "Execute a request in the swarm")
        {
            new Argument<string>("request", "The request to execute"),
            new Option<int>("--max-depth", () => 5, "Maximum tree depth"),
            new Option<int>("--max-tokens", () => 10000, "Maximum tokens to use"),
            new Option<decimal>("--max-cost", () => 1.0m, "Maximum cost in USD")
        };

        command.SetHandler(async (string request, int maxDepth, int maxTokens, decimal maxCost) =>
        {
            var orchestrator = services.GetRequiredService<IOrchestrator>();

            await AnsiConsole.Status()
                .StartAsync("Executing request...", async ctx =>
                {
                    var envelope = new RequestEnvelope
                    {
                        CorrelationId = Guid.NewGuid().ToString(),
                        Source = "cli",
                        Payload = request,
                        Timestamp = DateTimeOffset.UtcNow
                    };

                    var context = new ExecutionContext
                    {
                        CorrelationId = envelope.CorrelationId,
                        Budget = new Budget
                        {
                            MaxDepth = maxDepth,
                            MaxTokens = maxTokens,
                            MaxCost = maxCost
                        }
                    };

                    ctx.Status("Creating execution plan...");
                    var plan = await orchestrator.CreatePlanAsync(envelope, context);

                    ctx.Status($"Executing {plan.Tasks.Count} tasks...");
                    var result = await orchestrator.ExecutePlanAsync(plan, context);

                    AnsiConsole.WriteLine();

                    var statusColor = result.Status switch
                    {
                        PlanStatus.Completed => "green",
                        PlanStatus.PartiallyCompleted => "yellow",
                        PlanStatus.Failed => "red",
                        _ => "grey"
                    };

                    AnsiConsole.MarkupLine($"[{statusColor}]Status: {result.Status}[/]");
                    AnsiConsole.MarkupLine($"Tasks: {result.Tasks.Count(t => t.Status == TaskStatus.Completed)}/{result.Tasks.Count} completed");
                    AnsiConsole.MarkupLine($"Tokens: {context.TokensConsumed}");
                    AnsiConsole.MarkupLine($"Cost: {context.CostAccumulated:C}");

                    foreach (var task in result.Tasks.Where(t => t.Status == TaskStatus.Completed && !string.IsNullOrEmpty(t.Result)))
                    {
                        AnsiConsole.WriteLine();
                        AnsiConsole.Write(new Panel(task.Result!)
                        {
                            Header = new PanelHeader($"Task: {task.TaskId}"),
                            Border = BoxBorder.Rounded
                        });
                    }
                });
        },
        command.Arguments.OfType<Argument<string>>().First(),
        command.Options.OfType<Option<int>>().First(),
        command.Options.OfType<Option<int>>().Last(),
        command.Options.OfType<Option<decimal>>().First());

        return command;
    }
}
