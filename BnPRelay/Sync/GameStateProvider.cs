using System;
using System.Threading.Tasks;
using BnPRelay.Network;

namespace BnPRelay.Sync
{
    public struct OverworldStateData
    {
        public short RoomId;
        public byte InteractFlag;
        public short P1X;
        public short P1Y;
        public byte P1Sprite;
        public byte P1Frame;
        public short P2X;
        public short P2Y;
        public byte P2Sprite;
        public byte P2Frame;

        public bool Equals(OverworldStateData other) =>
            RoomId == other.RoomId &&
            InteractFlag == other.InteractFlag &&
            P1X == other.P1X && P1Y == other.P1Y &&
            P1Sprite == other.P1Sprite && P1Frame == other.P1Frame &&
            P2X == other.P2X && P2Y == other.P2Y &&
            P2Sprite == other.P2Sprite && P2Frame == other.P2Frame;
    }

    public struct CombatStateData
    {
        public byte TurnState;
        public byte TargetId;
        public short Damage;
        public short MonsterHp;
        public int Seed;
    }

    public struct HeartPositionData
    {
        public short P1X;
        public short P1Y;
        public byte P1SoulMode;
        public short P2X;
        public short P2Y;
        public byte P2SoulMode;
    }

    public struct PlayerHitData
    {
        public byte PlayerIndex;
        public short RemainingHp;
        public byte InvFrames;
    }

    /// <summary>
    /// Central Game State Provider managing overworld, combat, player injury, and heart positioning.
    /// Emits structured packets across the Virtual Network Session and logs diagnostics.
    /// </summary>
    public class GameStateProvider
    {
        private OverworldStateData _lastOverworld;
        private CombatStateData _lastCombat;
        private HeartPositionData _lastHeart;

        private DateTime _lastOverworldLog = DateTime.MinValue;
        private DateTime _lastHeartLog = DateTime.MinValue;

        public event Action<OverworldStateData>? OverworldStateChanged;
        public event Action<CombatStateData>? CombatEventDispatched;
        public event Action<HeartPositionData>? HeartPositionChanged;
        public event Action<PlayerHitData>? PlayerDamaged;
        public event Action<byte>? WaveFinished;

        public OverworldStateData CurrentOverworld => _lastOverworld;
        public CombatStateData CurrentCombat => _lastCombat;
        public HeartPositionData CurrentHeart => _lastHeart;

        /// <summary>
        /// Updates overworld coordinates, interaction flags, and sprite animation frames.
        /// </summary>
        public void UpdateOverworld(OverworldStateData data, bool forceBroadcast = false)
        {
            bool hasChanged = !data.Equals(_lastOverworld);
            if (hasChanged || forceBroadcast)
            {
                _lastOverworld = data;
                OverworldStateChanged?.Invoke(data);

                // Rate-limited logging (max once per second or immediately on room/interact change)
                if ((DateTime.UtcNow - _lastOverworldLog).TotalSeconds >= 1.0 ||
                    hasChanged && (data.RoomId != _lastOverworld.RoomId || data.InteractFlag != _lastOverworld.InteractFlag))
                {
                    _lastOverworldLog = DateTime.UtcNow;
                    Logger.Log($"[StateProvider] Overworld Sync -> Room: {data.RoomId}, Interact: {data.InteractFlag}, " +
                               $"P1: ({data.P1X},{data.P1Y}, Spr:{data.P1Sprite}), P2: ({data.P2X},{data.P2Y}, Spr:{data.P2Sprite})");
                }
            }
        }

        /// <summary>
        /// Dispatches a battle combat event (Damage dealt, monster HP update, RNG seed).
        /// </summary>
        public void DispatchCombatEvent(CombatStateData data)
        {
            _lastCombat = data;
            CombatEventDispatched?.Invoke(data);
            Logger.Log($"[StateProvider] Combat Event -> TurnState: {data.TurnState}, Target: {data.TargetId}, " +
                       $"Damage: {data.Damage}, MonsterHP: {data.MonsterHp}, Seed: {data.Seed}");
        }

        /// <summary>
        /// Updates battle soul/heart positions and soul modes (Red, Blue, Green, Purple).
        /// </summary>
        public void UpdateHeartPositions(HeartPositionData data)
        {
            _lastHeart = data;
            HeartPositionChanged?.Invoke(data);

            if ((DateTime.UtcNow - _lastHeartLog).TotalSeconds >= 2.0)
            {
                _lastHeartLog = DateTime.UtcNow;
                Logger.Log($"[StateProvider] Heart Sync -> P1 Heart: ({data.P1X},{data.P1Y}, Mode:{data.P1SoulMode}), " +
                           $"P2 Heart: ({data.P2X},{data.P2Y}, Mode:{data.P2SoulMode})");
            }
        }

        /// <summary>
        /// Client-authoritative injury notification.
        /// Broadcasts only when the local player's soul takes damage.
        /// </summary>
        public void ReportPlayerHit(byte playerIndex, short remainingHp, byte invFrames)
        {
            var hit = new PlayerHitData
            {
                PlayerIndex = playerIndex,
                RemainingHp = remainingHp,
                InvFrames = invFrames
            };

            PlayerDamaged?.Invoke(hit);
            Logger.Log($"[StateProvider] PLAYER HIT -> Player {playerIndex} injured! Remaining HP: {remainingHp}, InvFrames: {invFrames}");
        }

        /// <summary>
        /// Signals that the local battle wave timer reached 0 and the player finished dodging.
        /// </summary>
        public void ReportWaveFinished(byte turnIndex)
        {
            WaveFinished?.Invoke(turnIndex);
            Logger.Log($"[StateProvider] Wave Finished -> Turn Index {turnIndex} completed.");
        }
    }
}
