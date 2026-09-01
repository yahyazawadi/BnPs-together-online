using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using BnPRelay.Network;

namespace BnPRelay
{
    /// <summary>
    /// Client mode: connects to the host at a given ZeroTier IP on port 7777.
    /// Auto-reconnects on disconnect with exponential backoff.
    /// </summary>
    public class ClientSession : IDisposable
    {
        private const int Port = 7777;
        private const int MaxReconnectDelayMs = 8000;

        private readonly string _hostIp;
        private TcpClient? _client;
        private CancellationTokenSource _cts = new();

        public event Action<string>? StatusChanged;
        public event Action<int>?    LatencyUpdated;
        public event Action<InputBitmask>? RemoteInputReceived; // P1 input from host
        public event Action<int, byte>? TurnSeedReceived;       // (seed, turnIndex) — set RNG before ACK
        public event Action<byte>? AttackGoReceived;             // Both start attack now
        public event Action<string, byte[]>? SaveFileReceived;  // (filename, data)
        public event Action? PauseReceived;
        public event Action? ResumeReceived;
        public event Action? HostConnected;
        public event Action? HostDisconnected;

        public ClientSession(string hostIp) => _hostIp = hostIp;

        public async Task ConnectAsync()
        {
            int delay = 500;
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    StatusChanged?.Invoke($"Connecting to {_hostIp}:{Port}...");
                    _client = new TcpClient();
                    _client.NoDelay = true;
                    await _client.ConnectAsync(_hostIp, Port, _cts.Token);
                    ConfigureSocket(_client);
                    delay = 500;
                    StatusChanged?.Invoke("Connected!");
                    HostConnected?.Invoke();
                    await RunSessionAsync(_client, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch
                {
                    HostDisconnected?.Invoke();
                    StatusChanged?.Invoke($"Disconnected. Retrying in {delay / 1000.0:F1}s...");
                    await Task.Delay(delay, _cts.Token);
                    delay = Math.Min(delay * 2, MaxReconnectDelayMs);
                }
            }
        }

        public async Task SendInputAsync(InputBitmask mask)
        {
            if (_client?.Connected != true) return;
            await PacketFramer.SendAsync(_client.GetStream(), PacketType.Input,
                new[] { mask.Value }, _cts.Token);
        }

        public async Task SendSeedAckAsync(byte turnIndex)
        {
            if (_client?.Connected != true) return;
            await PacketFramer.SendAsync(_client.GetStream(), PacketType.SeedAck,
                new[] { turnIndex }, _cts.Token);
        }

        private async Task RunSessionAsync(TcpClient client, CancellationToken ct)
        {
            var stream = client.GetStream();
            var pingSent = DateTime.UtcNow;

            while (!ct.IsCancellationRequested)
            {
                var (type, payload) = await PacketFramer.ReceiveAsync(stream, ct);
                switch (type)
                {
                    case PacketType.Input:
                        RemoteInputReceived?.Invoke(InputBitmask.From(payload[0]));
                        break;

                    case PacketType.TurnSeed:
                        int seed = BitConverter.ToInt32(payload, 0);
                        byte turnIndex = payload[4];
                        TurnSeedReceived?.Invoke(seed, turnIndex);
                        // ACK immediately — host waits for this before firing AttackGo
                        await SendSeedAckAsync(turnIndex);
                        break;

                    case PacketType.AttackGo:
                        AttackGoReceived?.Invoke(payload[0]);
                        break;

                    case PacketType.SaveUpdate:
                        int nameLen = payload[0];
                        string fileName = System.Text.Encoding.UTF8.GetString(payload, 1, nameLen);
                        int dataLen = BitConverter.ToInt32(payload, 1 + nameLen);
                        byte[] data = new byte[dataLen];
                        Array.Copy(payload, 1 + nameLen + 4, data, 0, dataLen);
                        SaveFileReceived?.Invoke(fileName, data);
                        break;

                    case PacketType.PauseGame:
                        PauseReceived?.Invoke();
                        break;

                    case PacketType.ResumeGame:
                        ResumeReceived?.Invoke();
                        break;

                    case PacketType.Ping:
                        pingSent = DateTime.UtcNow;
                        await PacketFramer.SendAsync(stream, PacketType.Pong, Array.Empty<byte>(), ct);
                        break;

                    case PacketType.Pong:
                        LatencyUpdated?.Invoke((int)(DateTime.UtcNow - pingSent).TotalMilliseconds);
                        break;
                }
            }
        }

        private static void ConfigureSocket(TcpClient client)
        {
            client.NoDelay = true;
            client.ReceiveTimeout = 5000;
            client.SendTimeout = 2000;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _client?.Dispose();
        }
    }
}
