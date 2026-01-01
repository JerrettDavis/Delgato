using System.CommandLine;
using Delgato.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Delgato.Cli.Commands;

/// <summary>
/// Commands for health monitoring.
/// </summary>
public static class HealthCommand
{
    public static Command Create(IServiceProvider services)
    {
        var command = new Command("health", "Health monitoring commands");

        command.AddCommand(CreateCheckCommand(services));
        command.AddCommand(CreateMonitorCommand(services));

        return command;
    }

    private static Command CreateCheckCommand(IServiceProvider services)
    {
        var command = new Command("check", "Run health checks");

        command.SetHandler(async () =>
        {
            await AnsiConsole.Status()
                .StartAsync("Running health checks...", async ctx =>
                {
                    var checks = new List<(string Name, bool Healthy, string Details)>();

                    // Check agent registry
                    try
                    {
                        var registry = services.GetRequiredService<IAgentRegistry>();
                        var agents = await registry.GetAllDefinitionsAsync();
                        checks.Add(("Agent Registry", true, $"{agents.Count} agents registered"));
                    }
                    catch (Exception ex)
                    {
                        checks.Add(("Agent Registry", false, ex.Message));
                    }

                    // Check providers
                    try
                    {
                        var providerRegistry = services.GetRequiredService<IProviderRegistry>();
                        var providers = providerRegistry.GetAll();
                        var healthResults = await providerRegistry.CheckHealthAsync();
                        var healthyCount = healthResults.Count(r => r.Value.IsHealthy);
                        checks.Add(("Providers", healthyCount > 0, $"{healthyCount}/{providers.Count} healthy"));
                    }
                    catch (Exception ex)
                    {
                        checks.Add(("Providers", false, ex.Message));
                    }

                    // Check orchestrator
                    try
                    {
                        var orchestrator = services.GetRequiredService<IOrchestrator>();
                        checks.Add(("Orchestrator", true, orchestrator.GetType().Name));
                    }
                    catch (Exception ex)
                    {
                        checks.Add(("Orchestrator", false, ex.Message));
                    }

                    // Display results
                    var allHealthy = checks.All(c => c.Healthy);
                    var overallStatus = allHealthy ? "[green]HEALTHY[/]" : "[red]UNHEALTHY[/]";

                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"Overall Status: {overallStatus}\n");

                    var table = new Table()
                        .Border(TableBorder.Rounded)
                        .AddColumn("Component")
                        .AddColumn("Status")
                        .AddColumn("Details");

                    foreach (var (name, healthy, details) in checks)
                    {
                        var status = healthy ? "[green]● OK[/]" : "[red]● FAIL[/]";
                        table.AddRow(name, status, details);
                    }

                    AnsiConsole.Write(table);
                });
        });

        return command;
    }

    private static Command CreateMonitorCommand(IServiceProvider services)
    {
        var command = new Command("monitor", "Monitor system health continuously")
        {
            new Option<int>("--interval", () => 30, "Check interval in seconds")
        };

        command.SetHandler(async (int interval) =>
        {
            AnsiConsole.MarkupLine($"[grey]Monitoring health every {interval} seconds. Press Ctrl+C to stop.[/]\n");

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            var providerRegistry = services.GetRequiredService<IProviderRegistry>();

            while (!cts.Token.IsCancellationRequested)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                var healthResults = await providerRegistry.CheckHealthAsync(cts.Token);

                var status = new StringBuilder();
                status.Append($"[grey]{timestamp}[/] ");

                foreach (var (providerId, result) in healthResults)
                {
                    var icon = result.IsHealthy ? "[green]●[/]" : "[red]●[/]";
                    status.Append($"{icon} {providerId} ");
                }

                AnsiConsole.MarkupLine(status.ToString());

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(interval), cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            AnsiConsole.MarkupLine("\n[grey]Monitoring stopped.[/]");
        },
        command.Options.OfType<Option<int>>().First());

        return command;
    }
}

// StringBuilder is used in the monitor command
file static class StringBuilderExtension
{
    public static void Clear(this StringBuilder sb)
    {
        sb.Length = 0;
    }
}
