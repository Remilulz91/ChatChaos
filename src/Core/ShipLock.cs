using ChatChaos.Networking;
using UnityEngine;

namespace ChatChaos.Core
{
    /// <summary>
    /// "Ship locked": closes the hangar door and blocks the lever (no takeoff) for a
    /// duration, then reopens/unblocks. The host owns the timer; the lock/unlock is
    /// mirrored to every player by the networker.
    /// </summary>
    public static class ShipLock
    {
        private static bool _active;
        private static float _unlockTime;

        public static void Reset() => _active = false;

        /// <summary>Lock the ship for <paramref name="seconds"/> (host only).</summary>
        public static void Lock(float seconds)
        {
            var n = ChatChaosNetworker.Active;
            if (n == null)
            {
                Plugin.Log.LogWarning("ChatChaos: ship lock skipped (networker not ready).");
                return;
            }
            _active = true;
            _unlockTime = Time.time + seconds;
            n.SetShipLocked(true);
            n.ShowEffectTimer("shiplock", "fx.shiplock", seconds);
            Plugin.Log.LogInfo($"ChatChaos: ship locked for {seconds:0}s.");
        }

        /// <summary>Called every frame on the host; unlocks when the timer ends.</summary>
        public static void Tick()
        {
            if (!_active) return;
            if (Time.time >= _unlockTime)
            {
                _active = false;
                ChatChaosNetworker.Active?.SetShipLocked(false);
            }
        }
    }
}
