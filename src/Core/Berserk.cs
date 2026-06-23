using System.Collections.Generic;
using ChatChaos.Logging;
using ChatChaos.Networking;
using UnityEngine;

namespace ChatChaos.Core
{
    /// <summary>
    /// "Berserk" event: when it wins, a "RECEIVING SIGNAL -> GO BERSERK" overlay plays;
    /// once it finishes, a random living player becomes invincible for 45s.
    /// (The shotgun + unlimited ammo is added in a later version.)
    ///
    /// Timing is host-driven. The invincible-player index is mirrored to every machine
    /// (SetActivePlayer) because the damage patches run on all clients.
    /// </summary>
    public static class Berserk
    {
        private const float SignalDuration = 3.5f;   // keep >= the BerserkHud overlay length
        private const float BerserkDuration = 45f;

        private enum Phase { Idle, Signal, Active }

        // Per-machine: the invincible player's index (read by the damage/kill patches).
        private static int _activeIndex = -1;

        // Host-only timing.
        private static Phase _phase = Phase.Idle;
        private static int _pendingIndex = -1;
        private static float _phaseEndTime;

        public static void Reset()
        {
            _activeIndex = -1;
            _phase = Phase.Idle;
            _pendingIndex = -1;
            BerserkShotgun.Clear();
        }

        /// <summary>Set the invincible player on THIS machine (from the networker). -1 = none.</summary>
        public static void SetActivePlayer(int index) => _activeIndex = index;

        /// <summary>Called by the damage/kill patches. True = ignore the damage (invincible).</summary>
        public static bool IsInvincible(object player)
        {
            if (_activeIndex < 0) return false;
            var sor = StartOfRound.Instance;
            if (sor == null || sor.allPlayerScripts == null) return false;
            if (_activeIndex >= sor.allPlayerScripts.Length) return false;
            return ReferenceEquals(sor.allPlayerScripts[_activeIndex], player);
        }

        /// <summary>Host: the event won — show the signal, then go berserk.</summary>
        public static void Trigger()
        {
            if (_phase != Phase.Idle)
            {
                Log.Info("Berserk", "trigger ignored (already running).");
                return;
            }
            int idx = PickRandomAlive();
            if (idx < 0)
            {
                Log.Warn("Berserk", "no living player to go berserk.");
                return;
            }
            _pendingIndex = idx;
            _phase = Phase.Signal;
            _phaseEndTime = Time.time + SignalDuration;
            ChatChaosNetworker.Active?.ShowBerserkSignal();
            Log.Info("Berserk", $"RECEIVING SIGNAL shown; player index {idx} goes berserk in {SignalDuration:0.0}s.");
        }

        /// <summary>Host: drives signal -> active -> end. (Idle on clients, so it no-ops there.)</summary>
        public static void Tick()
        {
            if (_phase == Phase.Idle) return;
            if (Time.time < _phaseEndTime) return;

            if (_phase == Phase.Signal)
            {
                _phase = Phase.Active;
                _phaseEndTime = Time.time + BerserkDuration;
                ChatChaosNetworker.Active?.SetBerserkPlayer(_pendingIndex);
                ChatChaosNetworker.Active?.GiveBerserkShotgun(_pendingIndex);
                ChatChaosNetworker.Active?.ShowEffectTimer("berserk", "fx.berserk", BerserkDuration);
                Log.Info("Berserk", $"GO BERSERK — player index {_pendingIndex} invincible for {BerserkDuration:0}s.");
            }
            else // Active
            {
                _phase = Phase.Idle;
                ChatChaosNetworker.Active?.SetBerserkPlayer(-1);
                ChatChaosNetworker.Active?.RemoveBerserkShotgun();
                _pendingIndex = -1;
                Log.Info("Berserk", "berserk ended.");
            }
        }

        private static int PickRandomAlive()
        {
            var sor = StartOfRound.Instance;
            if (sor == null || sor.allPlayerScripts == null) return -1;

            var alive = new List<int>();
            for (int i = 0; i < sor.allPlayerScripts.Length; i++)
            {
                var p = sor.allPlayerScripts[i];
                if (p != null && p.isPlayerControlled && !p.isPlayerDead) alive.Add(i);
            }
            return alive.Count == 0 ? -1 : alive[Random.Range(0, alive.Count)];
        }
    }
}
