using Microsoft.AspNetCore.SignalR;

namespace Argus.Api.Hubs;

/// <summary>
/// SignalR hub for real-time risk data streaming.
/// Broadcasting is done via IHubContext from the consumer worker — the hub itself is passive.
/// Clients receive: "RiskUpdated" with a RiskSnapshot payload at ~1Hz.
/// </summary>
public sealed class RiskHub : Hub
{
    private readonly ILogger<RiskHub> _logger;

    public RiskHub(ILogger<RiskHub> logger)
    {
        _logger = logger;
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
