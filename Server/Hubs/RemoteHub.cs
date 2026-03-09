using Microsoft.AspNetCore.SignalR;
using RemoteDesk.Server.Services;

namespace RemoteDesk.Server.Hubs
{
    /// <summary>
    /// Central SignalR hub connecting browser clients and Windows PC agents.
    /// </summary>
    public class RemoteHub : Hub
    {
        private readonly PcRegistryService _registry;
        private readonly ILogger<RemoteHub> _logger;

        public RemoteHub(PcRegistryService registry, ILogger<RemoteHub> logger)
        {
            _registry = registry;
            _logger = logger;
        }

        // ── PC Agent → Server ─────────────────────────────────────────────

        /// <summary>Windows app calls this on connect to advertise its ID.</summary>
        public async Task RegisterPc(string pcId)
        {
            _registry.RegisterPc(pcId, Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, $"pc_{pcId}");
            _logger.LogInformation("PC registered: {PcId} ({ConnId})", pcId, Context.ConnectionId);

            // Notify all viewers watching this PC
            await Clients.Group($"viewers_{pcId}").SendAsync("PcOnline", pcId);
        }

        /// <summary>Windows app streams a JPEG frame encoded as base64.</summary>
        public async Task SendFrame(string pcId, string frameBase64)
        {
            // Forward to every browser watching this PC
            await Clients.Group($"viewers_{pcId}").SendAsync("ReceiveFrame", frameBase64);
        }

        // ── Browser Client → Server ───────────────────────────────────────

        /// <summary>Browser starts watching a PC.</summary>
        public async Task WatchPc(string pcId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"viewers_{pcId}");
            _logger.LogInformation("Viewer {ConnId} watching {PcId}", Context.ConnectionId, pcId);

            // Tell the PC agent to start streaming
            var pcConnId = _registry.GetConnectionId(pcId);
            if (pcConnId != null)
            {
                await Clients.Client(pcConnId).SendAsync("StartStream");
                await Clients.Caller.SendAsync("PcOnline", pcId);
            }
            else
            {
                await Clients.Caller.SendAsync("PcOffline", pcId);
            }
        }

        /// <summary>Browser stops watching a PC.</summary>
        public async Task StopWatching(string pcId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"viewers_{pcId}");

            // If no viewers remain, tell PC to pause streaming
            if (!_registry.HasViewers(pcId))
            {
                var pcConnId = _registry.GetConnectionId(pcId);
                if (pcConnId != null)
                    await Clients.Client(pcConnId).SendAsync("StopStream");
            }
        }

        /// <summary>Browser sends a mouse event to the PC.</summary>
        public async Task SendMouseEvent(string pcId, int x, int y, string eventType)
        {
            var pcConnId = _registry.GetConnectionId(pcId);
            if (pcConnId != null)
                await Clients.Client(pcConnId).SendAsync("MouseEvent", x, y, eventType);
        }

        /// <summary>Browser sends a keyboard event to the PC.</summary>
        public async Task SendKeyboardEvent(string pcId, string key, bool isKeyDown)
        {
            var pcConnId = _registry.GetConnectionId(pcId);
            if (pcConnId != null)
                await Clients.Client(pcConnId).SendAsync("KeyboardEvent", key, isKeyDown);
        }

        /// <summary>Returns all currently registered (online) PCs.</summary>
        public Task<IEnumerable<string>> GetOnlinePcs() =>
            Task.FromResult(_registry.GetAllPcIds());

        // ── Lifecycle ─────────────────────────────────────────────────────

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var pcId = _registry.GetPcIdByConnectionId(Context.ConnectionId);
            if (pcId != null)
            {
                _registry.UnregisterPc(pcId);
                _logger.LogInformation("PC disconnected: {PcId}", pcId);
                await Clients.Group($"viewers_{pcId}").SendAsync("PcOffline", pcId);
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}