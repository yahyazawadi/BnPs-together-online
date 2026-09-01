using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using BnPRelay.Network;
using BnPRelay.Sync;

namespace BnPRelay
{
    /// <summary>
    /// Host mode: listens for one client connection on TCP port 7777.
    /// Coordinates the session handshake: sends save file + seed, then begins
    /// bidirectional input relay once both sides confirm READY.
    /// </summary>
    public class HostSession : IDisposable
    {
        private const int Port = 7777;

        private TcpListener? _listener;
        private TcpClient?   _client;
        private CancellationTokenSource _cts = new();

        // Events raised on UI thread context
        public event Action<string>? StatusChanged;
        public event Action<int>?    LatencyUpdated;
        public event Action<InputBitmask>? RemoteInputReceived;
        public event Action? ClientConnected;
        public event Action? ClientDisconnected;
        public event Action? SeedAckReceived_Raw;

        public HeartbeatManager? Heartbeat { get; private set; }

        public async Task StartAsync()
        {
            _listener = new TcpListener(IPAddress.Any, Port);
            _listener.Start();
            Logger.Log($"[Host] Listening on port {Port}...");

            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    StatusChanged?.Invoke("Waiting for friend to connect...");
                    _client = await _listener.AcceptTcpClientAsync(_cts.Token);

                    ConfigureSocket(_client);
                    Logger.Log($"[Host] Client connected from {_client.Client.RemoteEndPoint}");
                    StatusChanged?.Invoke("Client connected — performing handshake...");
                    ClientConnected?.Invoke();

                    await RunSessionAsync(_client, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Logger.Log($"[Host] Session dropped: {ex.Message}");
                    ClientDisconnected?.Invoke();
                }
            }

            try { _listener.Stop(); } catch { }
        }

        public async Task SendInputAsync(InputBitmask mask)
        {
            if (_client?.Connected != true) return;
            var stream = _client.GetStream();
            await PacketFramer.SendAsync(stream, PacketType.Input, new[] { mask.Value }, _cts.Token);
        }

        public async Task SendTurnSeedAsync(int seed, byte turnIndex)
        {
            if (_client?.Connected != true) return;
            var stream = _client.GetStream();
            var payload = new byte[5];
            BitConverter.GetBytes(seed).CopyTo(payload, 0);
            payload[4] = turnIndex;
            await PacketFramer.SendAsync(stream, PacketType.TurnSeed, payload, _cts.Token);
        }

        public async Task SendAttackGoAsync(byte turnIndex)
        {
            if (_client?.Connected != true) return;
            var stream = _client.GetStream();
            await PacketFramer.SendAsync(stream, PacketType.AttackGo, new[] { turnIndex }, _cts.Token);
        }

        public async Task SendSaveFileAsync(string fileName, byte[] data)
        {
            if (_client?.Connected != true) return;
            var stream = _client.GetStream();
            var nameBytes = System.Text.Encoding.UTF8.GetBytes(fileName);
            var payload = new byte[1 + nameBytes.Length + 4 + data.Length];
            payload[0] = (byte)nameBytes.Length;
            nameBytes.CopyTo(payload, 1);
            BitConverter.GetBytes(data.Length).CopyTo(payload, 1 + nameBytes.Length);
            data.CopyTo(payload, 1 + nameBytes.Length + 4);
            await PacketFramer.SendAsync(stream, PacketType.SaveUpdate, payload, _cts.Token);
        }

        public async Task SendPauseAsync(bool paused)
        {
            if (_client?.Connected != true) return;
            var stream = _client.GetStream();
            await PacketFramer.SendAsync(stream, paused ? PacketType.PauseGame : PacketType.ResumeGame,
                Array.Empty<byte>(), _cts.Token);
        }

        private async Task RunSessionAsync(TcpClient client, CancellationToken ct)
        {
            var stream = client.GetStream();

            // Set up heartbeat manager
            Heartbeat = new HeartbeatManager(
                async () => await PacketFramer.SendAsync(stream, PacketType.Ping, Array.Empty<byte>(), ct));
            Heartbeat.Disconnected += () => ClientDisconnected?.Invoke();
            Heartbeat.Reconnected  += () => ClientConnected?.Invoke();
            Heartbeat.Start();

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var (type, payload) = await PacketFramer.ReceiveAsync(stream, ct);
                    switch (type)
                    {
                        case PacketType.Input:
                            RemoteInputReceived?.Invoke(InputBitmask.From(payload[0]));
                            break;
                        case PacketType.SeedAck:
                            // Client confirmed seed — fire AttackGo
                            await SendAttackGoAsync(payload[0]);
                            SeedAckReceived_Raw?.Invoke();
                            break;
                        case PacketType.Pong:
                            Heartbeat.OnPongReceived();
                            LatencyUpdated?.Invoke(Heartbeat.LatencyMs);
                            break;
                    }
                }
            }
            catch (Exception)
            {
                ClientDisconnected?.Invoke();
            }
            finally
            {
                Heartbeat.Dispose();
            }
        }

        private static void ConfigureSocket(TcpClient client)
        {
            client.NoDelay = true;           // Disable Nagle — inputs need immediate delivery
            client.ReceiveTimeout = 5000;
            client.SendTimeout = 2000;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _client?.Dispose();
            _listener?.Stop();
        }
    }
}
