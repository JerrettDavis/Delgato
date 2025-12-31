using Delgato.Core;
using Delgato.Core.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Delgato.Transports.Http;

/// <summary>
/// HTTP-based transport adapter for processing incoming requests via REST API.
/// </summary>
public sealed class HttpTransportAdapter : ITransportAdapter
{
    private readonly Channel<RequestEnvelope> _requestChannel;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _pendingResponses;
    private readonly ILogger<HttpTransportAdapter> _logger;
    private bool _isRunning;

    public string Name => "http";

    public HttpTransportAdapter(ILogger<HttpTransportAdapter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _requestChannel = Channel.CreateUnbounded<RequestEnvelope>();
        _pendingResponses = new ConcurrentDictionary<string, TaskCompletionSource<object>>();
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting HTTP transport adapter");
        _isRunning = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping HTTP transport adapter");
        _isRunning = false;
        _requestChannel.Writer.Complete();
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<RequestEnvelope> ReceiveAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var request in _requestChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return request;
        }
    }

    public ValueTask AcknowledgeAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Acknowledged request: {CorrelationId}", correlationId);
        return ValueTask.CompletedTask;
    }

    public ValueTask SendResponseAsync(
        string correlationId,
        object response,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending response for: {CorrelationId}", correlationId);

        if (_pendingResponses.TryRemove(correlationId, out var tcs))
        {
            tcs.SetResult(response);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Submits a request to be processed (called by HTTP endpoint).
    /// </summary>
    public async ValueTask<object> SubmitRequestAsync(
        object payload,
        IDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            throw new InvalidOperationException("Transport adapter is not running");
        }

        var correlationId = Guid.NewGuid().ToString();
        var envelope = new RequestEnvelope
        {
            CorrelationId = correlationId,
            Source = Name,
            Payload = payload,
            Metadata = metadata ?? new Dictionary<string, string>()
        };

        var tcs = new TaskCompletionSource<object>();
        _pendingResponses[correlationId] = tcs;

        await _requestChannel.Writer.WriteAsync(envelope, cancellationToken);

        _logger.LogInformation("Submitted request: {CorrelationId}", correlationId);

        // Wait for response with timeout
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            return await tcs.Task.WaitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            _pendingResponses.TryRemove(correlationId, out _);
            throw new TimeoutException($"Request {correlationId} timed out");
        }
    }
}

