using System.CommandLine;
using Delgato.Cli.Commands;
using Delgato.Cli.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace Delgato.Cli;

/// <summary>
/// Delgato CLI entry point.
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Display banner for interactive mode
        if (args.Length == 0 || args[0] == "interactive")
        {
            DisplayBanner();
        }

        var rootCommand = new RootCommand("Delgato Agent Swarm CLI - Enterprise agent orchestration platform")
        {
            Name = "delgato"
        };

        // Add global options
        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            description: "Enable verbose output");
        rootCommand.AddGlobalOption(verboseOption);

        var outputOption = new Option<OutputFormat>(
            aliases: new[] { "--output", "-o" },
            getDefaultValue: () => OutputFormat.Table,
            description: "Output format (table, json, yaml)");
        rootCommand.AddGlobalOption(outputOption);

        var configOption = new Option<string?>(
            aliases: new[] { "--config", "-c" },
            description: "Path to configuration file");
        rootCommand.AddGlobalOption(configOption);

        // Build service provider
        var services = ConfigureServices();

        // Add commands
        rootCommand.AddCommand(AgentCommand.Create(services));
        rootCommand.AddCommand(SwarmCommand.Create(services));
        rootCommand.AddCommand(ProviderCommand.Create(services));
        rootCommand.AddCommand(ConfigCommand.Create(services));
        rootCommand.AddCommand(AuditCommand.Create(services));
        rootCommand.AddCommand(HealthCommand.Create(services));
        rootCommand.AddCommand(InteractiveCommand.Create(services));
        rootCommand.AddCommand(RunCommand.Create(services));

        return await rootCommand.InvokeAsync(args);
    }

    private static void DisplayBanner()
    {
        AnsiConsole.Write(
            new FigletText("Delgato")
                .LeftJustified()
                .Color(Color.Blue));

        AnsiConsole.MarkupLine("[grey]Enterprise Agent Swarm Ecosystem[/]");
        AnsiConsole.MarkupLine("[grey]Version 1.0.0[/]");
        AnsiConsole.WriteLine();
    }

    private static IServiceProvider ConfigureServices()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddDelgatoCliServices();
            })
            .Build();

        return host.Services;
    }
}

/// <summary>
/// Output format for CLI commands.
/// </summary>
public enum OutputFormat
{
    Table,
    Json,
    Yaml
}
