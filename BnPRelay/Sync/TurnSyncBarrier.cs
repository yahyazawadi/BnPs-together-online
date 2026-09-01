using System;
using System.Threading;
using System.Threading.Tasks;
using BnPRelay.Sync;

namespace BnPRelay.Sync
{
    /// <summary>
    /// Coordinates the Turn-Sync Barrier between Host and Client for battle phases.
    ///
    /// Flow:
    ///   1. Host detects a new battle turn (via manual trigger or future auto-detection).
    ///   2. Host reads current RNG seed from memory → sends [TURN_SEED] packet.
    ///   3. BOTH instances are held in a "barrier" state — injection continues but
    ///      no new battle animations start on either side.
    ///   4. Client receives seed → writes it to own game memory → sends [SEED_ACK].
    ///   5. Host receives ACK → broadcasts [ATTACK_GO] to both sides.
    ///   6. Both instances release barrier simultaneously → attack begins on same frame.
    ///
    /// The attack animations are already built into the game engine; we just ensure
    /// both sides' RNG states are identical before the game's step events fire.
    ///
    /// Timeout: if ACK not received within 2 seconds, releases barrier anyway
    /// (prevents the game from hanging permanently on a bad network blip).
    /// </summary>
    public class TurnSyncBarrier : IDisposable
    {
        private const int AckTimeoutMs = 2000;

        private readonly MemoryManager _mem;
        private readonly Func<int, byte, Task> _sendTurnSeed;   // host sends to client
        private readonly Func<Task> _sendAttackGo;              // host broadcasts go

        private int _currentTurnIndex = 0;
        private TaskCompletionSource<bool>? _ackWaiter;
        private SemaphoreSlim _barrierLock = new(1, 1);

        /// <summary>
        /// Fired when the barrier is released — both sides should start the attack.
        /// Connect this to whatever UI feedback you need.
        /// </summary>
        public event Action? BarrierReleased;

        public TurnSyncBarrier(MemoryManager mem,
            Func<int, byte, Task> sendTurnSeed,
            Func<Task> sendAttackGo)
        {
            _mem = mem;
            _sendTurnSeed = sendTurnSeed;
            _sendAttackGo = sendAttackGo;
        }

        // ─── HOST SIDE ────────────────────────────────────────────────────────

        /// <summary>
        /// Called by Host when entering a new battle turn.
        /// Reads current RNG seed, sends to client, waits for ACK, then fires GO.
        /// </summary>
        public async Task HostStartTurnAsync()
        {
            await _barrierLock.WaitAsync();
            try
            {
                byte turnIndex = (byte)Interlocked.Increment(ref _currentTurnIndex);
                int seed = _mem.IsAttached ? _mem.ReadRngSeed() : Environment.TickCount;

                _ackWaiter = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                // Send seed to client
                await _sendTurnSeed(seed, turnIndex);

                // Wait for ACK with timeout
                var ack = _ackWaiter.Task;
                var timeout = Task.Delay(AckTimeoutMs);
                await Task.WhenAny(ack, timeout);

                // Whether ACK arrived or timed out, send GO
                await _sendAttackGo();
                BarrierReleased?.Invoke();
            }
            finally
            {
                _ackWaiter = null;
                _barrierLock.Release();
            }
        }

        /// <summary>
        /// Called when a [SEED_ACK] packet arrives from the client.
        /// Releases the host's barrier wait.
        /// </summary>
        public void OnSeedAckReceived(byte turnIndex)
        {
            _ackWaiter?.TrySetResult(true);
        }

        // ─── CLIENT SIDE ─────────────────────────────────────────────────────

        /// <summary>
        /// Called when a [TURN_SEED] packet arrives from the host.
        /// Writes seed to game memory. The ClientSession then sends ACK automatically.
        /// </summary>
        public void OnTurnSeedReceived(int seed, byte turnIndex)
        {
            if (_mem.IsAttached)
                _mem.WriteRngSeed(seed);
            // ACK is sent by ClientSession immediately after calling this
        }

        /// <summary>
        /// Called when [ATTACK_GO] arrives from host.
        /// At this point both sides have the same seed and attacks begin.
        /// </summary>
        public void OnAttackGoReceived(byte turnIndex)
        {
            BarrierReleased?.Invoke();
        }

        public void Dispose() => _barrierLock.Dispose();
    }
}
