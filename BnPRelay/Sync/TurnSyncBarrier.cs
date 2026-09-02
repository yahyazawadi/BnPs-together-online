using System;
using System.Threading;
using System.Threading.Tasks;
using BnPRelay.Network;

namespace BnPRelay.Sync
{
    /// <summary>
    /// Coordinates the Battle RNG Turn-Sync Barrier and Wave End Lockstep between Host and Client.
    ///
    /// Turn Flow:
    ///   1. Host detects battle dodging phase (global.myfight == 3).
    ///   2. Host generates deterministic 32-bit turn seed → sends [TURN_SEED] (0x10) to Client.
    ///   3. Client receives seed → seeds GameMaker RNG with random_set_seed → sends [SEED_ACK] (0x11).
    ///   4. Host receives ACK → sends [ATTACK_GO] (0x12) → both game instances spawn identical bullet patterns.
    ///
    /// Wave End Lockstep Flow:
    ///   1. When wave timer expires locally, each player reports WaveFinished (0x24).
    ///   2. The battle state machine waits until BOTH Host and Client have signaled WaveFinished.
    ///   3. Both release into the action selection menu (global.myfight = 0) simultaneously.
    /// </summary>
    public class TurnSyncBarrier : IDisposable
    {
        private const int AckTimeoutMs = 2500;
        private const int WaveEndTimeoutMs = 3000;

        private readonly MemoryManager _mem;
        private readonly Func<int, byte, Task> _sendTurnSeed;
        private readonly Func<byte, Task> _sendAttackGo;
        private readonly Func<CombatStateData, Task>? _sendCombatEvent;

        private int _currentTurnIndex = 0;
        private TaskCompletionSource<bool>? _ackWaiter;
        private TaskCompletionSource<bool>? _waveEndWaiter;
        private readonly SemaphoreSlim _barrierLock = new(1, 1);
        private System.Timers.Timer? _battleWatcherTimer;

        private bool _isHostTurnActive = false;
        private bool _isClientWaveFinished = false;
        private bool _isHostWaveFinished = false;

        public event Action? BarrierReleased;
        public event Action<byte>? TurnCompleted;
        public event Action<byte, short, short>? MonsterHpUpdated;

        public int CurrentTurnIndex => _currentTurnIndex;

        public TurnSyncBarrier(
            MemoryManager mem,
            Func<int, byte, Task> sendTurnSeed,
            Func<byte, Task> sendAttackGo,
            Func<CombatStateData, Task>? sendCombatEvent = null)
        {
            _mem = mem;
            _sendTurnSeed = sendTurnSeed;
            _sendAttackGo = sendAttackGo;
            _sendCombatEvent = sendCombatEvent;
        }

        // ─── HOST BATTLE COORDINATION ────────────────────────────────────────

        /// <summary>
        /// Called by Host when a new bullet wave begins (global.myfight == 3).
        /// Synchronizes the RNG seed across both machines.
        /// </summary>
        public async Task HostStartTurnAsync()
        {
            await _barrierLock.WaitAsync();
            try
            {
                byte turnIndex = (byte)Interlocked.Increment(ref _currentTurnIndex);
                int seed = _mem.IsAttached ? _mem.ReadRngSeed() : Environment.TickCount;
                if (seed == 0) seed = new Random().Next(100000, 999999);

                _isHostTurnActive = true;
                _isHostWaveFinished = false;
                _isClientWaveFinished = false;

                _ackWaiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                Logger.Log($"[TurnSyncBarrier] Host starting Turn #{turnIndex} with Seed {seed}...");

                // Write seed to Host's own memory first
                if (_mem.IsAttached)
                    _mem.WriteRngSeed(seed);

                // Broadcast TurnSeed packet to Client
                await _sendTurnSeed(seed, turnIndex);

                // Wait for Client ACK or timeout
                var ackTask = _ackWaiter.Task;
                var timeoutTask = Task.Delay(AckTimeoutMs);
                var completed = await Task.WhenAny(ackTask, timeoutTask);

                if (completed == timeoutTask)
                {
                    Logger.Log($"[TurnSyncBarrier] Client SEED_ACK timed out for Turn #{turnIndex} (releasing anyway).");
                }
                else
                {
                    Logger.Log($"[TurnSyncBarrier] Client confirmed SEED_ACK for Turn #{turnIndex}!");
                }

                // Send ATTACK_GO signal to begin attack phase simultaneously
                await _sendAttackGo(turnIndex);
                BarrierReleased?.Invoke();
            }
            finally
            {
                _ackWaiter = null;
                _barrierLock.Release();
            }
        }

        /// <summary>
        /// Host receives SEED_ACK from Client.
        /// </summary>
        public void OnSeedAckReceived(byte turnIndex)
        {
            Logger.Log($"[TurnSyncBarrier] OnSeedAckReceived for Turn #{turnIndex}");
            _ackWaiter?.TrySetResult(true);
        }

        /// <summary>
        /// Handles Client's WaveFinished packet on Host.
        /// </summary>
        public void OnClientWaveFinishedReceived(byte turnIndex)
        {
            _isClientWaveFinished = true;
            Logger.Log($"[TurnSyncBarrier] Client signaled WaveFinished for Turn #{turnIndex}. Host finished: {_isHostWaveFinished}");
            CheckWaveBarrierRelease(turnIndex);
        }

        /// <summary>
        /// Host's local wave timer expired.
        /// </summary>
        public void HostReportLocalWaveFinished(byte turnIndex)
        {
            _isHostWaveFinished = true;
            Logger.Log($"[TurnSyncBarrier] Host local WaveFinished for Turn #{turnIndex}. Client finished: {_isClientWaveFinished}");
            CheckWaveBarrierRelease(turnIndex);
        }

        private void CheckWaveBarrierRelease(byte turnIndex)
        {
            if (_isHostWaveFinished && _isClientWaveFinished)
            {
                _isHostTurnActive = false;
                _waveEndWaiter?.TrySetResult(true);
                Logger.Log($"[TurnSyncBarrier] BOTH players finished wave for Turn #{turnIndex}! Releasing to Menu.");
                TurnCompleted?.Invoke(turnIndex);
            }
        }

        // ─── CLIENT BATTLE COORDINATION ──────────────────────────────────────

        /// <summary>
        /// Called when Client receives TURN_SEED (0x10) from Host.
        /// Writes seed to GameMaker memory and triggers ACK.
        /// </summary>
        public void OnTurnSeedReceived(int seed, byte turnIndex)
        {
            _currentTurnIndex = turnIndex;
            Logger.Log($"[TurnSyncBarrier] Client received TurnSeed for Turn #{turnIndex}: {seed}");

            if (_mem.IsAttached)
            {
                bool ok = _mem.WriteRngSeed(seed);
                Logger.Log($"[TurnSyncBarrier] Seed written to Client game memory: {(ok ? "SUCCESS" : "FAILED")}");
            }
        }

        /// <summary>
        /// Called when Client receives ATTACK_GO (0x12) from Host.
        /// Releases local attack barrier.
        /// </summary>
        public void OnAttackGoReceived(byte turnIndex)
        {
            Logger.Log($"[TurnSyncBarrier] Client received AttackGo for Turn #{turnIndex}. Starting attack wave!");
            BarrierReleased?.Invoke();
        }

        // ─── MONSTER HP & COMBAT STATE ───────────────────────────────────────

        /// <summary>
        /// Broadcasts authoritative monster damage and remaining HP from Host to Client.
        /// </summary>
        public async Task BroadcastMonsterDamageAsync(byte targetId, short damage, short remainingHp, byte turnState = 1)
        {
            if (_sendCombatEvent != null)
            {
                var combat = new CombatStateData
                {
                    TurnState = turnState,
                    TargetId = targetId,
                    Damage = damage,
                    MonsterHp = remainingHp,
                    Seed = _currentTurnIndex
                };
                await _sendCombatEvent(combat);
                Logger.Log($"[TurnSyncBarrier] Broadcast Monster HP: Target {targetId} took {damage} dmg -> Remaining HP: {remainingHp}");
            }
        }

        /// <summary>
        /// Applies incoming Monster HP update on Client.
        /// </summary>
        public void ApplyRemoteMonsterDamage(byte targetId, short damage, short remainingHp)
        {
            MonsterHpUpdated?.Invoke(targetId, damage, remainingHp);
            Logger.Log($"[TurnSyncBarrier] Applied Remote Monster Damage: Target {targetId} took {damage} dmg -> New HP: {remainingHp}");
        }

        public void Dispose()
        {
            _battleWatcherTimer?.Dispose();
            _barrierLock.Dispose();
        }
    }
}
