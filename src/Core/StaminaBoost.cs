using UnityEngine;

namespace ChatChaos.Core
{
    /// <summary>
    /// "Stamina boost": boosted (but NOT unlimited) sprint for a duration. Sprinting is
    /// handled locally by each player, so every machine boosts its OWN player's sprint
    /// meter while the boost is active. Activated for everyone via the networker; the timer
    /// is per-machine (absolute Time.time), so it always expires on its own.
    /// </summary>
    public static class StaminaBoost
    {
        // Flat per-second top-up added to the sprint meter while the boost is active. It
        // partly offsets sprint drain (drains slower) and adds to regen (recovers faster).
        // Kept below the sprint drain rate so stamina stays limited, not infinite.
        // Tune higher for a stronger boost, lower for a subtler one.
        private const float BoostPerSecond = 0.06f;

        private static float _endTime;

        public static void Reset() => _endTime = 0f;

        /// <summary>Activate the boost on THIS machine for <paramref name="seconds"/>.</summary>
        public static void Activate(float seconds) => _endTime = Time.time + seconds;

        /// <summary>Called every frame on every machine; boosts the local player's stamina.</summary>
        public static void Tick()
        {
            if (Time.time >= _endTime) return;

            var p = StartOfRound.Instance != null ? StartOfRound.Instance.localPlayerController : null;
            if (p == null || !p.isPlayerControlled || p.isPlayerDead) return;

            // Boosted but limited: slower drain while sprinting, faster regen otherwise.
            p.sprintMeter = Mathf.Clamp01(p.sprintMeter + BoostPerSecond * Time.deltaTime);
        }
    }
}
