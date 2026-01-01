using System.CommandLine;
using Delgato.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Delgato.Cli.Commands;

/// <summary>
/// Commands for managing providers.
/// </summary>
public static class ProviderCommand
{
    public static Command Create(IServiceProvider services)
    {
        var command = new Command("provider", "Manage LLM providers");

        command.AddCommand(CreateListCommand(services));
        command.AddCommand(CreateHealthCommand(services));
        command.AddCommand(CreateModelsCommand(services));

        return command;
    }

    private static Command CreateListCommand(IServiceProvider services)
    {
        var command = new Command("list", "List all registered providers");

        command.SetHandler(() =>
        {
            var registry = services.GetRequiredService<IProviderRegistry>();
            var providers = registry.GetAll();

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Provider ID")
                .AddColumn("Display Name")
                .AddColumn("Models")
                .AddColumn("Capabilities");

            foreach (var provider in providers)
            {
                table.AddRow(
                    provider.ProviderId,
                    provider.DisplayName,
                    string.Join(", ", provider.SupportedModels.Take(3)) +
                        (provider.SupportedModels.Count > 3 ? $" (+{provider.SupportedModels.Count - 3})" : ""),
                    string.Join(", ", provider.Capabilities.Take(4))
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"\n[grey]Total: {providers.Count} providers[/]");
        });

        return command;
    }

    private static Command CreateHealthCommand(IServiceProvider services)
    {
        var command = new Command("health", "Check health of all providers");

        command.SetHandler(async () =>
        {
            var registry = services.GetRequiredService<IProviderRegistry>();

            await AnsiConsole.Status()
                .StartAsync("Checking provider health...", async ctx =>
                {
                    var results = await registry.CheckHealthAsync();

                    var table = new Table()
                        .Border(TableBorder.Rounded)
                        .AddColumn("Provider")
                        .AddColumn("Status")
                        .AddColumn("Latency")
                        .AddColumn("Details");

                    foreach (var (providerId, result) in results)
                    {
                        var status = result.IsHealthy
                            ? "[green]● Healthy[/]"
                            : "[red]● Unhealthy[/]";

                        var latency = result.Latency.HasValue
                            ? $"{result.Latency.Value.TotalMilliseconds:F0}ms"
                            : "N/A";

                        var details = result.ErrorMessage ?? result.Status;

                        table.AddRow(providerId, status, latency, details);
                    }

                    AnsiConsole.Write(table);
                });
        });

        return command;
    }

    private static Command CreateModelsCommand(IServiceProvider services)
    {
        var command = new Command("models", "List models for a provider")
        {
            new Argument<string>("provider", "Provider ID")
        };

        command.SetHandler((string providerId) =>
        {
            var registry = services.GetRequiredService<IProviderRegistry>();
            var provider = registry.GetById(providerId);

            if (provider == null)
            {
                AnsiConsole.MarkupLine($"[red]Provider '{providerId}' not found[/]");
                return;
            }

            AnsiConsole.MarkupLine($"[bold]{provider.DisplayName}[/] Models:\n");

            foreach (var model in provider.SupportedModels)
            {
                AnsiConsole.MarkupLine($"  • {model}");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold]Capabilities:[/]");
            foreach (var cap in provider.Capabilities)
            {
                AnsiConsole.MarkupLine($"  • {cap}");
            }
        },
        command.Arguments.OfType<Argument<string>>().First());

        return command;
    }
}
