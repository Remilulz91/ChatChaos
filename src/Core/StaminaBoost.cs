using UnityEngine;

namespace ChatChaos.Core
{
    /// <summary>
    /// "Stamina boost": unlimited sprint for a duration. Sprinting is handled locally by
    /// each player, so every machine keeps its OWN player's sprint meter full while the
    /// boost is active. Activated for everyone via the networker; the timer is per-machine
    /// (absolute Time.time), so it always expires on its own.
    /// </summary>
    public static class StaminaBoost
    {
        private static float _endTime;

        public static void Reset() => _endTime = 0f;

        /// <summary>Activate the boost on THIS machine for <paramref name="seconds"/>.</summary>
        public static void Activate(float seconds) => _endTime = Time.time + seconds;

        /// <summary>Called every frame on every machine; keeps the local player's stamina full.</summary>
        public static void Tick()
        {
            if (Time.time >= _endTime) return;

            var p = StartOfRound.Instance != null ? StartOfRound.Instance.localPlayerController : null;
            if (p == null || !p.isPlayerControlled || p.isPlayerDead) return;

            p.sprintMeter = 1f;   // keep full -> unlimited sprint
        }
    }
}
