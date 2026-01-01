using System.CommandLine;
using System.Text.Json;
using Spectre.Console;

namespace Delgato.Cli.Commands;

/// <summary>
/// Commands for configuration management.
/// </summary>
public static class ConfigCommand
{
    public static Command Create(IServiceProvider services)
    {
        var command = new Command("config", "Manage configuration");

        command.AddCommand(CreateShowCommand());
        command.AddCommand(CreateSetCommand());
        command.AddCommand(CreateListCommand());
        command.AddCommand(CreateInitCommand());

        return command;
    }

    private static Command CreateShowCommand()
    {
        var command = new Command("show", "Show current configuration");

        command.SetHandler(() =>
        {
            var configPath = GetConfigPath();

            if (!File.Exists(configPath))
            {
                AnsiConsole.MarkupLine("[grey]No configuration file found. Run 'delgato config init' to create one.[/]");
                return;
            }

            var config = JsonSerializer.Deserialize<Dictionary<string, object>>(
                File.ReadAllText(configPath),
                new JsonSerializerOptions { WriteIndented = true });

            if (config != null)
            {
                AnsiConsole.MarkupLine($"[bold]Configuration[/] ({configPath}):\n");

                foreach (var (key, value) in config)
                {
                    var displayValue = key.Contains("key", StringComparison.OrdinalIgnoreCase)
                        ? "***"
                        : value?.ToString() ?? "null";
                    AnsiConsole.MarkupLine($"  [blue]{key}[/]: {displayValue}");
                }
            }
        });

        return command;
    }

    private static Command CreateSetCommand()
    {
        var command = new Command("set", "Set a configuration value")
        {
            new Argument<string>("key", "Configuration key"),
            new Argument<string>("value", "Configuration value")
        };

        command.SetHandler((string key, string value) =>
        {
            var configPath = GetConfigPath();
            var config = new Dictionary<string, object>();

            if (File.Exists(configPath))
            {
                config = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    File.ReadAllText(configPath)) ?? new Dictionary<string, object>();
            }

            config[key] = value;

            var directory = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            }));

            AnsiConsole.MarkupLine($"[green]✓[/] Set [blue]{key}[/] = {(key.Contains("key", StringComparison.OrdinalIgnoreCase) ? "***" : value)}");
        },
        command.Arguments.OfType<Argument<string>>().First(),
        command.Arguments.OfType<Argument<string>>().Last());

        return command;
    }

    private static Command CreateListCommand()
    {
        var command = new Command("list", "List available configuration keys");

        command.SetHandler(() =>
        {
            var keys = new[]
            {
                ("agents_path", "Path to agent YAML files", "DELGATO_AGENTS_PATH"),
                ("anthropic_api_key", "Anthropic API key", "ANTHROPIC_API_KEY"),
                ("openai_api_key", "OpenAI API key", "OPENAI_API_KEY"),
                ("google_api_key", "Google API key", "GOOGLE_API_KEY"),
                ("default_provider", "Default LLM provider", "DELGATO_DEFAULT_PROVIDER"),
                ("default_model", "Default model ID", "DELGATO_DEFAULT_MODEL"),
                ("max_tokens", "Default max tokens", "DELGATO_MAX_TOKENS"),
                ("max_depth", "Default max tree depth", "DELGATO_MAX_DEPTH"),
                ("log_level", "Logging level", "DELGATO_LOG_LEVEL"),
                ("telemetry_enabled", "Enable telemetry", "DELGATO_TELEMETRY")
            };

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Key")
                .AddColumn("Description")
                .AddColumn("Environment Variable");

            foreach (var (key, description, envVar) in keys)
            {
                var currentValue = Environment.GetEnvironmentVariable(envVar);
                var status = string.IsNullOrEmpty(currentValue) ? "[grey]not set[/]" : "[green]set[/]";
                table.AddRow(key, description, $"{envVar} {status}");
            }

            AnsiConsole.Write(table);
        });

        return command;
    }

    private static Command CreateInitCommand()
    {
        var command = new Command("init", "Initialize configuration interactively");

        command.SetHandler(() =>
        {
            AnsiConsole.MarkupLine("[bold]Delgato Configuration Setup[/]\n");

            var providers = new Dictionary<string, string>();

            // Anthropic
            if (AnsiConsole.Confirm("Configure Anthropic Claude?", false))
            {
                var key = AnsiConsole.Prompt(
                    new TextPrompt<string>("Enter Anthropic API key:")
                        .Secret());
                providers["anthropic_api_key"] = key;
            }

            // OpenAI
            if (AnsiConsole.Confirm("Configure OpenAI?", false))
            {
                var key = AnsiConsole.Prompt(
                    new TextPrompt<string>("Enter OpenAI API key:")
                        .Secret());
                providers["openai_api_key"] = key;
            }

            // Google
            if (AnsiConsole.Confirm("Configure Google Gemini?", false))
            {
                var key = AnsiConsole.Prompt(
                    new TextPrompt<string>("Enter Google API key:")
                        .Secret());
                providers["google_api_key"] = key;
            }

            // Agents path
            var agentsPath = AnsiConsole.Prompt(
                new TextPrompt<string>("Agents directory path:")
                    .DefaultValue("./agents"));

            providers["agents_path"] = agentsPath;

            // Save configuration
            var configPath = GetConfigPath();
            var directory = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(configPath, JsonSerializer.Serialize(providers, new JsonSerializerOptions
            {
                WriteIndented = true
            }));

            AnsiConsole.MarkupLine($"\n[green]✓[/] Configuration saved to: [bold]{configPath}[/]");
            AnsiConsole.MarkupLine("\n[grey]Tip: You can also set environment variables for API keys[/]");
        });

        return command;
    }

    private static string GetConfigPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".delgato", "config.json");
    }
}
