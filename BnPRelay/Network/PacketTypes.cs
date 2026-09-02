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
        public const byte Input             = 0x01; // 6-key bitmask (1 byte payload)
        public const byte TurnSeed          = 0x10; // Host->Client: battle seed (4 bytes + 1 byte turn index)
        public const byte SeedAck           = 0x11; // Client->Host: seed received ACK (1 byte turn index)
        public const byte AttackGo          = 0x12; // Host->Client: BOTH start attack NOW (1 byte turn index)
        public const byte RoomSync          = 0x20; // Host->Client: room_id + coordinates (10 bytes)
        public const byte OverworldState    = 0x21; // Overworld animation, interaction, and player state (14 bytes)
        public const byte CombatEvent       = 0x22; // Battle turn state, target, damage, monster HP (10 bytes)
        public const byte PlayerHit         = 0x23; // Client-authoritative hit registration (4 bytes)
        public const byte WaveFinished      = 0x24; // Turn-end lockstep barrier signal (1 byte)
        public const byte HeartPositionSync = 0x25; // Battle heart positions & soul modes (10 bytes)
        public const byte SaveUpdate        = 0x30; // Host->Client: save file update
        public const byte Ping              = 0x40;
        public const byte Pong              = 0x41;
        public const byte PauseGame         = 0x42; // Freeze all injection on receiver
        public const byte ResumeGame        = 0x43;
        public const byte SessionReady      = 0x50; // Both sides ready to launch game
        public const byte RemoteLog         = 0x60; // Bidirectional streaming log message (UTF-8 string)
    }

    /// <summary>
    /// Binary serializer/deserializer helpers for high-frequency game state packets.
    /// </summary>
    public static class PacketSerializer
    {
        // ─── OVERWORLD STATE (14 bytes) ─────────────────────────────────────────
        public static byte[] EncodeOverworld(short roomId, byte interactFlag, short p1X, short p1Y, byte p1Sprite, byte p1Frame, short p2X, short p2Y, byte p2Sprite, byte p2Frame)
        {
            byte[] buf = new byte[14];
            BitConverter.GetBytes(roomId).CopyTo(buf, 0);
            buf[2] = interactFlag;
            BitConverter.GetBytes(p1X).CopyTo(buf, 3);
            BitConverter.GetBytes(p1Y).CopyTo(buf, 5);
            buf[7] = p1Sprite;
            buf[8] = p1Frame;
            BitConverter.GetBytes(p2X).CopyTo(buf, 9);
            BitConverter.GetBytes(p2Y).CopyTo(buf, 11);
            buf[13] = p2Sprite;
            return buf;
        }

        public static (short roomId, byte interactFlag, short p1X, short p1Y, byte p1Sprite, byte p1Frame, short p2X, short p2Y, byte p2Sprite, byte p2Frame) DecodeOverworld(byte[] buf)
        {
            if (buf.Length < 14) return (0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            return (
                BitConverter.ToInt16(buf, 0),
                buf[2],
                BitConverter.ToInt16(buf, 3),
                BitConverter.ToInt16(buf, 5),
                buf[7],
                buf[8],
                BitConverter.ToInt16(buf, 9),
                BitConverter.ToInt16(buf, 11),
                buf[13],
                buf.Length > 14 ? buf[14] : (byte)0
            );
        }

        // ─── COMBAT EVENT (10 bytes) ────────────────────────────────────────────
        public static byte[] EncodeCombatEvent(byte turnState, byte targetId, short damage, short monsterHp, int seed)
        {
            byte[] buf = new byte[10];
            buf[0] = turnState;
            buf[1] = targetId;
            BitConverter.GetBytes(damage).CopyTo(buf, 2);
            BitConverter.GetBytes(monsterHp).CopyTo(buf, 4);
            BitConverter.GetBytes(seed).CopyTo(buf, 6);
            return buf;
        }

        public static (byte turnState, byte targetId, short damage, short monsterHp, int seed) DecodeCombatEvent(byte[] buf)
        {
            if (buf.Length < 10) return (0, 0, 0, 0, 0);
            return (
                buf[0],
                buf[1],
                BitConverter.ToInt16(buf, 2),
                BitConverter.ToInt16(buf, 4),
                BitConverter.ToInt32(buf, 6)
            );
        }

        // ─── PLAYER HIT (4 bytes) ───────────────────────────────────────────────
        public static byte[] EncodePlayerHit(byte playerIndex, short remainingHp, byte invFrames)
        {
            byte[] buf = new byte[4];
            buf[0] = playerIndex;
            BitConverter.GetBytes(remainingHp).CopyTo(buf, 1);
            buf[3] = invFrames;
            return buf;
        }

        public static (byte playerIndex, short remainingHp, byte invFrames) DecodePlayerHit(byte[] buf)
        {
            if (buf.Length < 4) return (0, 0, 0);
            return (
                buf[0],
                BitConverter.ToInt16(buf, 1),
                buf[3]
            );
        }

        // ─── HEART POSITION SYNC (10 bytes) ─────────────────────────────────────
        public static byte[] EncodeHeartPosition(short p1X, short p1Y, byte p1SoulMode, short p2X, short p2Y, byte p2SoulMode)
        {
            byte[] buf = new byte[10];
            BitConverter.GetBytes(p1X).CopyTo(buf, 0);
            BitConverter.GetBytes(p1Y).CopyTo(buf, 2);
            buf[4] = p1SoulMode;
            BitConverter.GetBytes(p2X).CopyTo(buf, 5);
            BitConverter.GetBytes(p2Y).CopyTo(buf, 7);
            buf[9] = p2SoulMode;
            return buf;
        }

        public static (short p1X, short p1Y, byte p1SoulMode, short p2X, short p2Y, byte p2SoulMode) DecodeHeartPosition(byte[] buf)
        {
            if (buf.Length < 10) return (0, 0, 0, 0, 0, 0);
            return (
                BitConverter.ToInt16(buf, 0),
                BitConverter.ToInt16(buf, 2),
                buf[4],
                BitConverter.ToInt16(buf, 5),
                BitConverter.ToInt16(buf, 7),
                buf[9]
            );
        }
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
