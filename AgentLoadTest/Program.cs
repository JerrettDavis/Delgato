﻿using Delgato.Agents;
using Delgato.Configuration;

// Test agent loading directly
var agentDirectory = Path.GetFullPath(@"G:\git\Delgato\agents");

Console.WriteLine($"Testing agent loading from: {agentDirectory}");
Console.WriteLine($"Directory exists: {Directory.Exists(agentDirectory)}");

if (Directory.Exists(agentDirectory))
{
    var files = Directory.GetFiles(agentDirectory, "*.yaml");
    Console.WriteLine($"Found {files.Length} YAML files:");
    foreach (var file in files)
    {
        Console.WriteLine($"  - {Path.GetFileName(file)}");
    }
    
    Console.WriteLine("\nTesting AgentFileLoader...");
    var loader = new AgentFileLoader();
    
    try
    {
        var definitions = await loader.LoadFromDirectoryAsync(agentDirectory);
        Console.WriteLine($"\n✓ Successfully loaded {definitions.Count} agents:");
        foreach (var def in definitions)
        {
            Console.WriteLine($"  - {def.Id}: {def.Name}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n✗ Error loading agents: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            Console.WriteLine($"Inner stack trace: {ex.InnerException.StackTrace}");
        }
    }
    
    Console.WriteLine("\nTesting FileBasedAgentRegistry...");
    var registry = new FileBasedAgentRegistry(agentDirectory);
    
    try
    {
        await registry.InitializeAsync();
        var allAgents = await registry.GetAllDefinitionsAsync();
        Console.WriteLine($"\n✓ Registry loaded {allAgents.Count} agents:");
        foreach (var agent in allAgents)
        {
            Console.WriteLine($"  - {agent.Id}: {agent.Name}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n✗ Error in registry: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            Console.WriteLine($"Inner stack trace: {ex.InnerException.StackTrace}");
        }
    }
}
else
{
    Console.WriteLine("✗ Directory does not exist!");
}

