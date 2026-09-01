using System;
using System.Threading.Tasks;

namespace BnPRelay.Sync
{
    /// <summary>
    /// Handles room transition coordinate synchronization.
    ///
    /// From binary analysis of data.win, P2 auto-teleports when P1 touches a door.
    /// The Together mod handles room transitions internally — we do NOT need to
    /// simulate P2 walking to the door.
    ///
    /// This class handles two remaining edge cases:
    ///   1. Auto-scrolling rooms (Mettaton jetpack, MTT News) where the camera
    ///      forces movement. We switch to high-frequency (10Hz) position sync
    ///      to prevent P2 being crushed by the scrolling boundary.
    ///   2. Position drift correction on room entry — Host broadcasts spawn
    ///      coordinates so Client hard-snaps P2 to the correct position.
    ///
    /// The class monitors room ID changes and fires the appropriate sync mode.
    /// </summary>
    public class RoomPositionSync : IDisposable
    {
        private readonly MemoryManager _mem;
        private readonly Func<byte[], Task> _sendRoomSync;  // sends RoomSync packet
        private System.Timers.Timer? _highFreqTimer;

        // Auto-scroller room IDs known from data.win analysis.
        // (Mettaton news, bomb defusal, jetpack segments)
        // These room IDs are approximate — confirmed against BnP's room layout.
        private static readonly int[] AutoScrollerRooms = { 270, 271, 272, 290, 291 };

        private int _lastRoomId = -1;
        private bool _inAutoScroller = false;

        public event Action<int>? RoomChanged;         // (new room id)
        public event Action? AutoScrollerEntered;
        public event Action? AutoScrollerExited;

        public RoomPositionSync(MemoryManager mem, Func<byte[], Task> sendRoomSync)
        {
            _mem = mem;
            _sendRoomSync = sendRoomSync;
        }

        /// <summary>
        /// Starts polling memory for room ID changes every 200ms.
        /// When a change is detected, broadcasts a RoomSync packet with coordinates.
        /// </summary>
        public void StartPolling()
        {
            var timer = new System.Timers.Timer(200);
            timer.Elapsed += async (_, _) => await CheckRoomChangeAsync();
            timer.Start();
            _highFreqTimer = timer;
        }

        private async Task CheckRoomChangeAsync()
        {
            // Room ID and coordinates are read from game memory.
            // Addresses are determined at runtime by MemoryManager pattern scan.
            // For now we use a placeholder — actual addresses added after Phase 0 test.
            if (!_mem.IsAttached) return;

            // TODO Phase 2b: read room_id from memory when address is calibrated
            // int roomId = _mem.ReadInt32(roomIdAddress);
            // if (roomId == _lastRoomId) return;
            // _lastRoomId = roomId;
            // RoomChanged?.Invoke(roomId);
            // ...

            await Task.CompletedTask;
        }

        /// <summary>
        /// Encodes a room sync payload for the RoomSync packet (0x20).
        /// Called by Host when a room transition is detected.
        /// </summary>
        public static byte[] BuildRoomSyncPayload(short roomId, short p1x, short p1y, short p2x, short p2y)
        {
            byte[] buf = new byte[10];
            BitConverter.GetBytes(roomId).CopyTo(buf, 0);
            BitConverter.GetBytes(p1x).CopyTo(buf, 2);
            BitConverter.GetBytes(p1y).CopyTo(buf, 4);
            BitConverter.GetBytes(p2x).CopyTo(buf, 6);
            BitConverter.GetBytes(p2y).CopyTo(buf, 8);
            return buf;
        }

        /// <summary>
        /// Parses an incoming RoomSync payload (Client side).
        /// </summary>
        public static (short roomId, short p1x, short p1y, short p2x, short p2y) ParseRoomSyncPayload(byte[] buf)
        {
            return (
                BitConverter.ToInt16(buf, 0),
                BitConverter.ToInt16(buf, 2),
                BitConverter.ToInt16(buf, 4),
                BitConverter.ToInt16(buf, 6),
                BitConverter.ToInt16(buf, 8)
            );
        }

        private bool IsAutoScrollerRoom(int roomId)
        {
            foreach (var r in AutoScrollerRooms)
                if (r == roomId) return true;
            return false;
        }

        public void Dispose() => _highFreqTimer?.Dispose();
    }
}
