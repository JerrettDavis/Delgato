using System.Collections.Concurrent;
using Delgato.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Delgato.Providers;

/// <summary>
/// Thread-safe registry for managing agent providers.
/// Supports dynamic registration and discovery of providers.
/// </summary>
public sealed class ProviderRegistry : IProviderRegistry
{
    private readonly ConcurrentDictionary<string, IAgentProvider> _providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<ProviderRegistry> _logger;

    public ProviderRegistry(ILogger<ProviderRegistry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public IReadOnlyList<IAgentProvider> GetAll()
    {
        return _providers.Values.ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public IAgentProvider? GetById(string providerId)
    {
        _providers.TryGetValue(providerId, out var provider);
        return provider;
    }

    /// <inheritdoc/>
    public IReadOnlyList<IAgentProvider> GetByCapability(string capability)
    {
        return _providers.Values
            .Where(p => p.Capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc/>
    public IReadOnlyList<IAgentProvider> GetByModel(string modelId)
    {
        return _providers.Values
            .Where(p => p.SupportedModels.Any(m => m.Equals(modelId, StringComparison.OrdinalIgnoreCase) ||
                                                   modelId.StartsWith(m, StringComparison.OrdinalIgnoreCase)))
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc/>
    public void Register(IAgentProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (_providers.TryAdd(provider.ProviderId, provider))
        {
            _logger.LogInformation(
                "Registered provider: {ProviderId} ({DisplayName}) with {ModelCount} models",
                provider.ProviderId,
                provider.DisplayName,
                provider.SupportedModels.Count);
        }
        else
        {
            _logger.LogWarning("Provider {ProviderId} already registered, updating", provider.ProviderId);
            _providers[provider.ProviderId] = provider;
        }
    }

    /// <inheritdoc/>
    public bool Unregister(string providerId)
    {
        if (_providers.TryRemove(providerId, out var removed))
        {
            _logger.LogInformation("Unregistered provider: {ProviderId}", providerId);
            return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, ProviderHealthResult>> CheckHealthAsync(CancellationToken ct = default)
    {
        var results = new Dictionary<string, ProviderHealthResult>();
        var tasks = _providers.Select(async kvp =>
        {
            try
            {
                var result = await kvp.Value.ValidateConnectionAsync(ct);
                return (kvp.Key, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed for provider {ProviderId}", kvp.Key);
                return (kvp.Key, new ProviderHealthResult
                {
                    IsHealthy = false,
                    Status = "Error",
                    ErrorMessage = ex.Message
                });
            }
        });

        var completedTasks = await Task.WhenAll(tasks);
        foreach (var (key, result) in completedTasks)
        {
            results[key] = result;
        }

        return results;
    }
}
