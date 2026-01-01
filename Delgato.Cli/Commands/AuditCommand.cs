using System.CommandLine;
using Delgato.Core.Governance;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Delgato.Cli.Commands;

/// <summary>
/// Commands for audit log management.
/// </summary>
public static class AuditCommand
{
    public static Command Create(IServiceProvider services)
    {
        var command = new Command("audit", "Manage audit logs");

        command.AddCommand(CreateSearchCommand(services));
        command.AddCommand(CreateStatsCommand(services));
        command.AddCommand(CreateExportCommand(services));

        return command;
    }

    private static Command CreateSearchCommand(IServiceProvider services)
    {
        var command = new Command("search", "Search audit events")
        {
            new Option<string?>("--type", "Filter by event type"),
            new Option<string?>("--actor", "Filter by actor ID"),
            new Option<string?>("--resource", "Filter by resource type"),
            new Option<string?>("--correlation", "Filter by correlation ID"),
            new Option<int>("--limit", () => 20, "Maximum number of results")
        };

        command.SetHandler(async (string? type, string? actor, string? resource, string? correlation, int limit) =>
        {
            var auditService = services.GetRequiredService<IAuditService>();

            var query = new AuditQuery
            {
                EventTypes = type != null ? new[] { type } : null,
                ActorId = actor,
                ResourceType = resource,
                CorrelationId = correlation,
                PageSize = limit,
                SortDescending = true
            };

            var result = await auditService.QueryAsync(query);

            if (result.Events.Count == 0)
            {
                AnsiConsole.MarkupLine("[grey]No audit events found[/]");
                return;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Timestamp")
                .AddColumn("Type")
                .AddColumn("Actor")
                .AddColumn("Resource")
                .AddColumn("Action")
                .AddColumn("Outcome");

            foreach (var evt in result.Events)
            {
                var outcomeColor = evt.Outcome switch
                {
                    AuditOutcome.Success => "green",
                    AuditOutcome.Failure => "red",
                    AuditOutcome.Denied => "yellow",
                    _ => "grey"
                };

                table.AddRow(
                    evt.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    evt.EventType,
                    evt.ActorId[..Math.Min(15, evt.ActorId.Length)],
                    $"{evt.ResourceType}/{evt.ResourceId[..Math.Min(10, evt.ResourceId.Length)]}",
                    evt.Action,
                    $"[{outcomeColor}]{evt.Outcome}[/]"
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"\n[grey]Showing {result.Events.Count} of {result.TotalCount} events[/]");
        },
        command.Options.OfType<Option<string?>>().ElementAt(0),
        command.Options.OfType<Option<string?>>().ElementAt(1),
        command.Options.OfType<Option<string?>>().ElementAt(2),
        command.Options.OfType<Option<string?>>().ElementAt(3),
        command.Options.OfType<Option<int>>().First());

        return command;
    }

    private static Command CreateStatsCommand(IServiceProvider services)
    {
        var command = new Command("stats", "Show audit statistics")
        {
            new Option<int>("--days", () => 7, "Number of days to include")
        };

        command.SetHandler(async (int days) =>
        {
            var auditService = services.GetRequiredService<IAuditService>();

            var to = DateTimeOffset.UtcNow;
            var from = to.AddDays(-days);

            var stats = await auditService.GetStatisticsAsync(from, to);

            var panel = new Panel(new Rows(
                new Markup($"[bold]Period:[/] {from:yyyy-MM-dd} to {to:yyyy-MM-dd}"),
                new Markup($"[bold]Total Events:[/] [green]{stats.TotalEvents:N0}[/]"),
                new Markup($"[bold]Success Rate:[/] [blue]{stats.SuccessRate:P1}[/]"),
                new Markup($"[bold]Avg Duration:[/] {stats.AverageDuration.TotalMilliseconds:F0}ms"),
                new Rule("[bold]By Category[/]")
            ))
            {
                Header = new PanelHeader("Audit Statistics"),
                Border = BoxBorder.Rounded
            };

            AnsiConsole.Write(panel);

            if (stats.EventsByCategory.Any())
            {
                var chart = new BarChart()
                    .Width(60)
                    .Label("[bold]Events by Category[/]");

                foreach (var (category, count) in stats.EventsByCategory.OrderByDescending(x => x.Value).Take(5))
                {
                    chart.AddItem(category.ToString(), count, Color.Blue);
                }

                AnsiConsole.Write(chart);
            }

            if (stats.EventsByOutcome.Any())
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold]By Outcome:[/]");
                foreach (var (outcome, count) in stats.EventsByOutcome)
                {
                    var color = outcome switch
                    {
                        AuditOutcome.Success => "green",
                        AuditOutcome.Failure => "red",
                        _ => "grey"
                    };
                    AnsiConsole.MarkupLine($"  [{color}]{outcome}[/]: {count:N0}");
                }
            }
        },
        command.Options.OfType<Option<int>>().First());

        return command;
    }

    private static Command CreateExportCommand(IServiceProvider services)
    {
        var command = new Command("export", "Export audit logs")
        {
            new Argument<string>("output", "Output file path"),
            new Option<string>("--format", () => "json", "Export format (json, csv)"),
            new Option<int>("--days", () => 30, "Number of days to export")
        };

        command.SetHandler(async (string output, string format, int days) =>
        {
            var auditService = services.GetRequiredService<IAuditService>();

            var to = DateTimeOffset.UtcNow;
            var from = to.AddDays(-days);

            var exportFormat = format.ToLowerInvariant() switch
            {
                "csv" => AuditExportFormat.Csv,
                _ => AuditExportFormat.Json
            };

            var request = new AuditExportRequest
            {
                Query = new AuditQuery
                {
                    FromTimestamp = from,
                    ToTimestamp = to
                },
                Format = exportFormat
            };

            await AnsiConsole.Status()
                .StartAsync("Exporting audit logs...", async ctx =>
                {
                    using var stream = await auditService.ExportAsync(request);
                    using var fileStream = File.Create(output);
                    await stream.CopyToAsync(fileStream);

                    AnsiConsole.MarkupLine($"[green]✓[/] Exported to: [bold]{output}[/]");
                });
        },
        command.Arguments.OfType<Argument<string>>().First(),
        command.Options.OfType<Option<string>>().First(),
        command.Options.OfType<Option<int>>().First());

        return command;
    }
}
