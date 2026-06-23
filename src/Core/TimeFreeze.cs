using ChatChaos.Networking;
using UnityEngine;

namespace ChatChaos.Core
{
    /// <summary>
    /// "Time frozen": stops the in-game day clock for a duration, then resumes it. The
    /// host owns the timer; the freeze/resume itself is mirrored to every player by the
    /// networker. One freeze at a time (re-picking the event while active just keeps it
    /// frozen; it resumes when the original timer ends).
    /// </summary>
    public static class TimeFreeze
    {
        private static bool _active;
        private static float _unfreezeTime;

        /// <summary>Clear the timer at game start (so a freeze never carries over).</summary>
        public static void Reset() => _active = false;

        /// <summary>Freeze the clock for <paramref name="seconds"/> (host only).</summary>
        public static void Freeze(float seconds)
        {
            var n = ChatChaosNetworker.Active;
            if (n == null)
            {
                Plugin.Log.LogWarning("ChatChaos: time freeze skipped (networker not ready).");
                return;
            }
            _active = true;
            _unfreezeTime = Time.time + seconds;
            n.SetTimeFrozen(true);
            Plugin.Log.LogInfo($"ChatChaos: time freeze for {seconds:0}s.");
        }

        /// <summary>Called every frame on the host; resumes time when the timer ends.</summary>
        public static void Tick()
        {
            if (!_active) return;
            if (Time.time >= _unfreezeTime)
            {
                _active = false;
                ChatChaosNetworker.Active?.SetTimeFrozen(false);
            }
        }
    }
}
