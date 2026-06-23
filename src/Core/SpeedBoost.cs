using UnityEngine;

namespace ChatChaos.Core
{
    /// <summary>
    /// "Fast & Serious": boosts every player's movement speed for a duration, then restores
    /// the original speed. Movement is local, so each machine boosts its OWN player. The
    /// original speed is captured once (so re-triggering can't lose it), the timer is
    /// per-machine (absolute Time.time) and always expires on its own.
    /// </summary>
    public static class SpeedBoost
    {
        // Movement speed multiplier while active. Tune higher for faster, lower for subtler.
        private const float Multiplier = 1.8f;

        private static float _endTime;
        private static float _originalSpeed;
        private static bool _active;   // per-machine: have we captured + applied the boost

        public static void Reset()
        {
            _endTime = 0f;
            if (_active)
            {
                var p = StartOfRound.Instance != null ? StartOfRound.Instance.localPlayerController : null;
                if (p != null) p.movementSpeed = _originalSpeed;
                _active = false;
            }
        }

        /// <summary>Activate the boost on THIS machine for <paramref name="seconds"/>.</summary>
        public static void Activate(float seconds) => _endTime = Time.time + seconds;

        /// <summary>Called every frame on every machine; applies/removes the speed boost.</summary>
        public static void Tick()
        {
            var p = StartOfRound.Instance != null ? StartOfRound.Instance.localPlayerController : null;
            bool shouldBoost = Time.time < _endTime && p != null && p.isPlayerControlled && !p.isPlayerDead;

            if (shouldBoost)
            {
                if (!_active)
                {
                    _originalSpeed = p.movementSpeed;   // capture the true base once
                    _active = true;
                }
                p.movementSpeed = _originalSpeed * Multiplier;
            }
            else if (_active)
            {
                if (p != null) p.movementSpeed = _originalSpeed;   // restore
                _active = false;
            }
        }
    }
}
