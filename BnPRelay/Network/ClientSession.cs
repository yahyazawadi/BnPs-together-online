using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using BnPRelay.Network;
using BnPRelay.Sync;

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
        public event Action<string>? ConnectionFailed;

        // New Game State Events
        public event Action<OverworldStateData>? OverworldStateReceived;
        public event Action<CombatStateData>? CombatEventReceived;
        public event Action<PlayerHitData>? PlayerHitReceived;
        public event Action<byte>? WaveFinishedReceived;
        public event Action<HeartPositionData>? HeartPositionReceived;
        public event Action<string>? RemoteLogReceived;

        public ClientSession(string hostIp)
        {
            _hostIp = hostIp;
            Logger.LogEmitted += OnLocalLogEmitted;
        }

        private void OnLocalLogEmitted(string logLine)
        {
            if (_client?.Connected == true && !logLine.Contains("[Client-Remote]"))
            {
                _ = SendLogMessageAsync(logLine);
            }
        }

        public async Task ConnectAsync()
        {
            int delay = 500;
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    Logger.Log($"[Client] Connecting to {_hostIp}:{Port}...");
                    StatusChanged?.Invoke($"Connecting to {_hostIp}:{Port}...");
                    _client = new TcpClient();
                    _client.NoDelay = true;
                    await _client.ConnectAsync(_hostIp, Port, _cts.Token);
                    ConfigureSocket(_client);
                    delay = 500;
                    Logger.Log($"[Client] Successfully connected to {_hostIp}:{Port}");
                    StatusChanged?.Invoke("Connected!");
                    HostConnected?.Invoke();
                    await RunSessionAsync(_client, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    string reason = ex.Message;
                    if (ex is System.Net.Sockets.SocketException sockEx)
                    {
                        reason = sockEx.SocketErrorCode switch
                        {
                            System.Net.Sockets.SocketError.ConnectionRefused => "Connection Refused (Host app is not running or port 7777 blocked)",
                            System.Net.Sockets.SocketError.TimedOut => "Connection Timed Out (IP unreachable or firewall blocking)",
                            System.Net.Sockets.SocketError.HostUnreachable => "Host Unreachable (Check ZeroTier / network connection)",
                            _ => sockEx.Message
                        };
                    }

                    Logger.Log($"[Client] Connection error to {_hostIp}: {reason}");
                    HostDisconnected?.Invoke();
                    ConnectionFailed?.Invoke(reason);
                    StatusChanged?.Invoke($"Disconnected ({reason}). Retrying in {delay / 1000.0:F1}s...");
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

        public async Task SendOverworldStateAsync(OverworldStateData state)
        {
            if (_client?.Connected != true) return;
            byte[] payload = PacketSerializer.EncodeOverworld(
                state.RoomId, state.InteractFlag,
                state.P1X, state.P1Y, state.P1Sprite, state.P1Frame,
                state.P2X, state.P2Y, state.P2Sprite, state.P2Frame
            );
            await PacketFramer.SendAsync(_client.GetStream(), PacketType.OverworldState, payload, _cts.Token);
        }

        public async Task SendCombatEventAsync(CombatStateData combat)
        {
            if (_client?.Connected != true) return;
            byte[] payload = PacketSerializer.EncodeCombatEvent(
                combat.TurnState, combat.TargetId, combat.Damage, combat.MonsterHp, combat.Seed
            );
            await PacketFramer.SendAsync(_client.GetStream(), PacketType.CombatEvent, payload, _cts.Token);
        }

        public async Task SendHeartPositionSyncAsync(HeartPositionData heart)
        {
            if (_client?.Connected != true) return;
            byte[] payload = PacketSerializer.EncodeHeartPosition(
                heart.P1X, heart.P1Y, heart.P1SoulMode,
                heart.P2X, heart.P2Y, heart.P2SoulMode
            );
            await PacketFramer.SendAsync(_client.GetStream(), PacketType.HeartPositionSync, payload, _cts.Token);
        }

        public async Task SendPlayerHitAsync(PlayerHitData hit)
        {
            if (_client?.Connected != true) return;
            byte[] payload = PacketSerializer.EncodePlayerHit(hit.PlayerIndex, hit.RemainingHp, hit.InvFrames);
            await PacketFramer.SendAsync(_client.GetStream(), PacketType.PlayerHit, payload, _cts.Token);
            Logger.Log($"[Client] Sent local PlayerHit: P{hit.PlayerIndex} remaining HP: {hit.RemainingHp}");
        }

        public async Task SendWaveFinishedAsync(byte turnIndex)
        {
            if (_client?.Connected != true) return;
            await PacketFramer.SendAsync(_client.GetStream(), PacketType.WaveFinished, new[] { turnIndex }, _cts.Token);
            Logger.Log($"[Client] Sent WaveFinished signal for turn {turnIndex}");
        }

        public async Task SendSeedAckAsync(byte turnIndex)
        {
            if (_client?.Connected != true) return;
            await PacketFramer.SendAsync(_client.GetStream(), PacketType.SeedAck,
                new[] { turnIndex }, _cts.Token);
        }

        public async Task SendLogMessageAsync(string message)
        {
            if (_client?.Connected != true) return;
            try
            {
                byte[] payload = System.Text.Encoding.UTF8.GetBytes(message);
                await PacketFramer.SendAsync(_client.GetStream(), PacketType.RemoteLog, payload, _cts.Token);
            }
            catch { }
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

                    case PacketType.RemoteLog:
                        string rLog = System.Text.Encoding.UTF8.GetString(payload);
                        RemoteLogReceived?.Invoke(rLog);
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

                    case PacketType.OverworldState:
                        var (roomId, interact, p1x, p1y, p1s, p1f, p2x, p2y, p2s, p2f) = PacketSerializer.DecodeOverworld(payload);
                        OverworldStateReceived?.Invoke(new OverworldStateData
                        {
                            RoomId = roomId,
                            InteractFlag = interact,
                            P1X = p1x, P1Y = p1y, P1Sprite = p1s, P1Frame = p1f,
                            P2X = p2x, P2Y = p2y, P2Sprite = p2s, P2Frame = p2f
                        });
                        break;

                    case PacketType.CombatEvent:
                        var (turnState, targetId, dmg, hp, cSeed) = PacketSerializer.DecodeCombatEvent(payload);
                        CombatEventReceived?.Invoke(new CombatStateData
                        {
                            TurnState = turnState, TargetId = targetId, Damage = dmg, MonsterHp = hp, Seed = cSeed
                        });
                        break;

                    case PacketType.PlayerHit:
                        var (pIdx, remHp, inv) = PacketSerializer.DecodePlayerHit(payload);
                        Logger.Log($"[Client] Received PlayerHit from Host: P{pIdx} HP -> {remHp}");
                        PlayerHitReceived?.Invoke(new PlayerHitData
                        {
                            PlayerIndex = pIdx, RemainingHp = remHp, InvFrames = inv
                        });
                        break;

                    case PacketType.WaveFinished:
                        Logger.Log($"[Client] Received WaveFinished from Host for Turn {payload[0]}");
                        WaveFinishedReceived?.Invoke(payload[0]);
                        break;

                    case PacketType.HeartPositionSync:
                        var (hP1x, hP1y, hP1m, hP2x, hP2y, hP2m) = PacketSerializer.DecodeHeartPosition(payload);
                        HeartPositionReceived?.Invoke(new HeartPositionData
                        {
                            P1X = hP1x, P1Y = hP1y, P1SoulMode = hP1m,
                            P2X = hP2x, P2Y = hP2y, P2SoulMode = hP2m
                        });
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
