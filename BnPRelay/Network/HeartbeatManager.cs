using System;
using System.Threading;
using System.Threading.Tasks;

namespace BnPRelay.Network
{
    /// <summary>
    /// Sends pings every second and tracks time since last pong.
    /// After 3 consecutive missed heartbeats (~3 seconds), fires Disconnected.
    /// After reconnection, fires Reconnected and re-enables input injection.
    /// </summary>
    public class HeartbeatManager : IDisposable
    {
        private const int PingIntervalMs     = 1000;
        private const int MissedPingsAllowed = 3;

        private readonly Func<Task> _sendPing;   // e.g. () => session.SendPingAsync()
        private CancellationTokenSource _cts = new();
        private DateTime _lastPongReceived = DateTime.UtcNow;
        private bool _isConnected = true;

        /// <summary>Fired when 3 consecutive pings are missed. Safe to call UI updates.</summary>
        public event Action? Disconnected;

        /// <summary>Fired when a pong arrives after a disconnect state.</summary>
        public event Action? Reconnected;

        /// <summary>Current round-trip latency in milliseconds (-1 if unknown).</summary>
        public int LatencyMs { get; private set; } = -1;

        private DateTime _lastPingSent;

        public HeartbeatManager(Func<Task> sendPing)
        {
            _sendPing = sendPing;
        }

        public void Start()
        {
            _ = RunAsync(_cts.Token);
        }

        private async Task RunAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(PingIntervalMs, ct);

                _lastPingSent = DateTime.UtcNow;
                try { await _sendPing(); }
                catch { /* socket gone — timeout will catch it */ }

                // Check timeout
                var elapsed = DateTime.UtcNow - _lastPongReceived;
                bool timedOut = elapsed.TotalMilliseconds > PingIntervalMs * MissedPingsAllowed;

                if (timedOut && _isConnected)
                {
                    _isConnected = false;
                    Disconnected?.Invoke();
                }
            }
        }

        /// <summary>Call this whenever a Pong packet is received from the remote.</summary>
        public void OnPongReceived()
        {
            LatencyMs = (int)(DateTime.UtcNow - _lastPingSent).TotalMilliseconds;
            _lastPongReceived = DateTime.UtcNow;

            if (!_isConnected)
            {
                _isConnected = true;
                Reconnected?.Invoke();
            }
        }

        public void Dispose() => _cts.Cancel();
    }
}
