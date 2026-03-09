using System.Collections.Concurrent;

namespace RemoteDesk.Server.Services
{
    /// <summary>
    /// Thread-safe in-memory registry of connected PC agents.
    /// </summary>
    public class PcRegistryService
    {
        // pcId → connectionId
        private readonly ConcurrentDictionary<string, string> _pcConnections = new();
        // connectionId → pcId  (reverse lookup)
        private readonly ConcurrentDictionary<string, string> _connToPc = new();
        // pcId → set of viewer connectionIds
        private readonly ConcurrentDictionary<string, ConcurrentHashSet<string>> _viewers = new();

        public void RegisterPc(string pcId, string connectionId)
        {
            _pcConnections[pcId] = connectionId;
            _connToPc[connectionId] = pcId;
            _viewers.TryAdd(pcId, new ConcurrentHashSet<string>());
        }

        public void UnregisterPc(string pcId)
        {
            if (_pcConnections.TryRemove(pcId, out var connId))
                _connToPc.TryRemove(connId, out _);
            _viewers.TryRemove(pcId, out _);
        }

        public string? GetConnectionId(string pcId) =>
            _pcConnections.TryGetValue(pcId, out var v) ? v : null;

        public string? GetPcIdByConnectionId(string connectionId) =>
            _connToPc.TryGetValue(connectionId, out var v) ? v : null;

        public IEnumerable<string> GetAllPcIds() => _pcConnections.Keys;

        public bool HasViewers(string pcId) =>
            _viewers.TryGetValue(pcId, out var set) && set.Count > 0;

        public void AddViewer(string pcId, string viewerConnId) =>
            _viewers.GetOrAdd(pcId, _ => new ConcurrentHashSet<string>()).Add(viewerConnId);

        public void RemoveViewer(string pcId, string viewerConnId)
        {
            if (_viewers.TryGetValue(pcId, out var set)) set.Remove(viewerConnId);
        }
    }

    /// <summary>Simple thread-safe HashSet wrapper.</summary>
    public class ConcurrentHashSet<T> where T : notnull
    {
        private readonly ConcurrentDictionary<T, byte> _dict = new();
        public bool Add(T item) => _dict.TryAdd(item, 0);
        public bool Remove(T item) => _dict.TryRemove(item, out _);
        public int Count => _dict.Count;
        public bool Contains(T item) => _dict.ContainsKey(item);
    }
}