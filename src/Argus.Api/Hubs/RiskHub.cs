using Microsoft.AspNetCore.SignalR;

namespace Argus.Api.Hubs;

/// <summary>
/// SignalR hub for real-time risk data streaming.
/// Broadcasting is done via IHubContext from the consumer worker — the hub itself is passive.
/// Clients receive: "RiskUpdated" with a RiskSnapshot payload at ~1Hz.
/// </summary>
public sealed class RiskHub : Hub
{
    private static int _activeConnections;

    /// <summary>
    /// Current number of connected SignalR clients. Read by ObservableGauge at scrape time.
    /// </summary>
    public static int ActiveConnections => _activeConnections;

    private readonly ILogger<RiskHub> _logger;

    public RiskHub(ILogger<RiskHub> logger)
    {
        _logger = logger;
    }

    public override Task OnConnectedAsync()
    {
        Interlocked.Increment(ref _activeConnections);
        _logger.LogInformation("Client connected: {ConnectionId} (active: {Count})",
            Context.ConnectionId, _activeConnections);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        Interlocked.Decrement(ref _activeConnections);
        _logger.LogInformation("Client disconnected: {ConnectionId} (active: {Count})",
            Context.ConnectionId, _activeConnections);
        return base.OnDisconnectedAsync(exception);
    }
}
