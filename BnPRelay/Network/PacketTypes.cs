using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace BnPRelay.Network
{
    /// <summary>
    /// Packet type identifiers.
    /// </summary>
    public static class PacketType
    {
        public const byte Input          = 0x01; // 6-key bitmask (1 byte payload)
        public const byte TurnSeed       = 0x10; // Host->Client: battle seed (4 bytes + 1 byte turn index)
        public const byte SeedAck        = 0x11; // Client->Host: seed received ACK (1 byte turn index)
        public const byte AttackGo       = 0x12; // Host->Client: BOTH start attack NOW (1 byte turn index)
        public const byte RoomSync       = 0x20; // Host->Client: room_id + coordinates (9 bytes)
        public const byte SaveUpdate     = 0x30; // Host->Client: save file update
        public const byte Ping           = 0x40;
        public const byte Pong           = 0x41;
        public const byte PauseGame      = 0x42; // Freeze all injection on receiver
        public const byte ResumeGame     = 0x43;
        public const byte SessionReady   = 0x50; // Both sides ready to launch game
    }

    /// <summary>
    /// Low-level framing: writes and reads length-prefixed packets over a TCP stream.
    /// Format: [1 byte type] [2 byte payload length] [N bytes payload]
    /// </summary>
    public static class PacketFramer
    {
        public static async Task SendAsync(NetworkStream stream, byte type, byte[] payload, CancellationToken ct)
        {
            int total = 3 + payload.Length;
            byte[] frame = new byte[total];
            frame[0] = type;
            frame[1] = (byte)(payload.Length >> 8);
            frame[2] = (byte)(payload.Length & 0xFF);
            payload.CopyTo(frame, 3);
            await stream.WriteAsync(frame, ct);
        }

        public static async Task<(byte type, byte[] payload)> ReceiveAsync(NetworkStream stream, CancellationToken ct)
        {
            byte[] header = new byte[3];
            await ReadExactAsync(stream, header, ct);
            byte type = header[0];
            int length = (header[1] << 8) | header[2];
            byte[] payload = new byte[length];
            if (length > 0) await ReadExactAsync(stream, payload, ct);
            return (type, payload);
        }

        private static async Task ReadExactAsync(NetworkStream stream, byte[] buf, CancellationToken ct)
        {
            int read = 0;
            while (read < buf.Length)
                read += await stream.ReadAsync(buf.AsMemory(read), ct);
        }
    }
}
