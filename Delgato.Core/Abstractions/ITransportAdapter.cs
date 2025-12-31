namespace Delgato.Core.Abstractions;

/// <summary>
/// Transport adapter for ingesting requests from external sources.
/// </summary>
public interface ITransportAdapter
{
    string Name { get; }
    
    ValueTask StartAsync(CancellationToken cancellationToken = default);
    ValueTask StopAsync(CancellationToken cancellationToken = default);
    
    IAsyncEnumerable<RequestEnvelope> ReceiveAsync(CancellationToken cancellationToken = default);
    
    ValueTask AcknowledgeAsync(
        string correlationId,
        CancellationToken cancellationToken = default);
    
    ValueTask SendResponseAsync(
        string correlationId,
        object response,
        CancellationToken cancellationToken = default);
}

