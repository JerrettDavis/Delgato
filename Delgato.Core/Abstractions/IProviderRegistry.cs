namespace Delgato.Core.Abstractions;

/// <summary>
/// Registry for managing agent providers.
/// Supports dynamic provider registration and discovery.
/// </summary>
public interface IProviderRegistry
{
    /// <summary>
    /// Gets all registered providers.
    /// </summary>
    IReadOnlyList<IAgentProvider> GetAll();

    /// <summary>
    /// Gets a provider by its ID.
    /// </summary>
    IAgentProvider? GetById(string providerId);

    /// <summary>
    /// Gets providers that support a specific capability.
    /// </summary>
    IReadOnlyList<IAgentProvider> GetByCapability(string capability);

    /// <summary>
    /// Gets providers that support a specific model.
    /// </summary>
    IReadOnlyList<IAgentProvider> GetByModel(string modelId);

    /// <summary>
    /// Registers a new provider.
    /// </summary>
    void Register(IAgentProvider provider);

    /// <summary>
    /// Unregisters a provider.
    /// </summary>
    bool Unregister(string providerId);

    /// <summary>
    /// Checks health of all providers.
    /// </summary>
    Task<IReadOnlyDictionary<string, ProviderHealthResult>> CheckHealthAsync(CancellationToken ct = default);
}

/// <summary>
/// Configuration for a provider.
/// </summary>
public sealed record ProviderConfiguration
{
    public required string ProviderId { get; init; }
    public required string ApiKey { get; init; }
    public string? Endpoint { get; init; }
    public string? Organization { get; init; }
    public int? TimeoutSeconds { get; init; }
    public int? MaxRetries { get; init; }
    public IDictionary<string, object> CustomSettings { get; init; } = new Dictionary<string, object>();
}
