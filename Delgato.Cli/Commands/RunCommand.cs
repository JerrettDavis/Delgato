using System.CommandLine;
using Delgato.Core;
using Delgato.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Delgato.Cli.Commands;

/// <summary>
/// Quick run command for executing requests.
/// </summary>
public static class RunCommand
{
    public static Command Create(IServiceProvider services)
    {
        var command = new Command("run", "Run a request through the swarm")
        {
            new Argument<string>("request", "The request to execute"),
            new Option<string?>("--agent", "Specific agent to use"),
            new Option<bool>("--stream", "Stream the response"),
            new Option<int>("--max-tokens", () => 4000, "Maximum tokens"),
            new Option<bool>("--json", "Output as JSON")
        };

        command.SetHandler(async (string request, string? agentId, bool stream, int maxTokens, bool json) =>
        {
            var registry = services.GetRequiredService<IAgentRegistry>();
            var factory = services.GetRequiredService<IAgentFactory>();
            var orchestrator = services.GetRequiredService<IOrchestrator>();

            AgentDefinition? agent = null;

            if (!string.IsNullOrEmpty(agentId))
            {
                agent = await registry.GetDefinitionAsync(new AgentId(agentId));
                if (agent == null)
                {
                    if (json)
                    {
                        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                        {
                            success = false,
                            error = $"Agent '{agentId}' not found"
                        }));
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]Agent '{agentId}' not found[/]");
                    }
                    return;
                }
            }

            var context = new ExecutionContext
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Budget = new Budget { MaxTokens = maxTokens }
            };

            if (agent != null)
            {
                // Direct agent execution
                if (json)
                {
                    var agentInstance = await factory.CreateAgentAsync(agent);
                    var response = await agentInstance.ExecuteAsync(request, context);

                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                    {
                        success = response.IsSuccess,
                        content = response.Content,
                        error = response.ErrorMessage,
                        tokensUsed = response.TokensUsed,
                        metadata = response.Metadata
                    }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    await AnsiConsole.Status()
                        .StartAsync($"Running agent {agent.Name}...", async ctx =>
                        {
                            var agentInstance = await factory.CreateAgentAsync(agent);
                            var response = await agentInstance.ExecuteAsync(request, context);

                            AnsiConsole.WriteLine();

                            if (response.IsSuccess)
                            {
                                AnsiConsole.Write(new Panel(response.Content)
                                {
                                    Header = new PanelHeader($"Response from {agent.Name}"),
                                    Border = BoxBorder.Rounded
                                });
                                AnsiConsole.MarkupLine($"\n[grey]Tokens: {response.TokensUsed}[/]");
                            }
                            else
                            {
                                AnsiConsole.MarkupLine($"[red]Error:[/] {response.ErrorMessage}");
                            }
                        });
                }
            }
            else
            {
                // Orchestrated execution
                var envelope = new RequestEnvelope
                {
                    CorrelationId = context.CorrelationId,
                    Source = "cli",
                    Payload = request,
                    Timestamp = DateTimeOffset.UtcNow
                };

                if (json)
                {
                    var plan = await orchestrator.CreatePlanAsync(envelope, context);
                    var result = await orchestrator.ExecutePlanAsync(plan, context);

                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                    {
                        success = result.Status == PlanStatus.Completed,
                        status = result.Status.ToString(),
                        tasks = result.Tasks.Select(t => new
                        {
                            id = t.TaskId,
                            status = t.Status.ToString(),
                            result = t.Result,
                            error = t.ErrorMessage
                        }),
                        tokensUsed = context.TokensConsumed,
                        cost = context.CostAccumulated
                    }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    await AnsiConsole.Status()
                        .StartAsync("Executing request...", async ctx =>
                        {
                            ctx.Status("Creating plan...");
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

                            foreach (var task in result.Tasks.Where(t => !string.IsNullOrEmpty(t.Result)))
                            {
                                AnsiConsole.WriteLine();
                                AnsiConsole.Write(new Panel(task.Result!)
                                {
                                    Border = BoxBorder.Rounded
                                });
                            }

                            AnsiConsole.MarkupLine($"\n[grey]Tokens: {context.TokensConsumed} | Cost: {context.CostAccumulated:C}[/]");
                        });
                }
            }
        },
        command.Arguments.OfType<Argument<string>>().First(),
        command.Options.OfType<Option<string?>>().First(),
        command.Options.OfType<Option<bool>>().First(),
        command.Options.OfType<Option<int>>().First(),
        command.Options.OfType<Option<bool>>().Last());

        return command;
    }
}
