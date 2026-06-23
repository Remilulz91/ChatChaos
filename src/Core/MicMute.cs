using ChatChaos.Logging;
using ChatChaos.Networking;
using UnityEngine;

namespace ChatChaos.Core
{
    /// <summary>
    /// "Mute the mic": mutes the HOST's in-game voice (Dissonance) for a duration, then
    /// unmutes. The event runs on the host (the streamer), so this is a purely local
    /// action — no networking needed. Done by reflection so we don't compile-depend on
    /// the Dissonance assembly.
    /// </summary>
    public static class MicMute
    {
        private static float _endTime;
        private static bool _active;

        public static void Reset()
        {
            _endTime = 0f;
            if (_active)
            {
                SetMuted(false);
                _active = false;
            }
        }

        /// <summary>Mute the local (host) mic for <paramref name="seconds"/>.</summary>
        public static void Mute(float seconds)
        {
            _endTime = Time.time + seconds;
            _active = true;
            SetMuted(true);
            ChatChaosNetworker.Active?.ShowEffectTimer("micmute", "fx.micmute", seconds);
            Log.Info("Mic", $"host mic muted for {seconds:0}s.");
        }

        /// <summary>Called every frame on the host; unmutes when the timer ends.</summary>
        public static void Tick()
        {
            if (!_active) return;
            if (Time.time >= _endTime)
            {
                SetMuted(false);
                _active = false;
                Log.Info("Mic", "host mic unmuted.");
            }
        }

        private static void SetMuted(bool muted)
        {
            try
            {
                var type = HarmonyLib.AccessTools.TypeByName("Dissonance.DissonanceComms");
                if (type == null)
                {
                    Log.Warn("Mic", "DissonanceComms type not found — cannot mute.");
                    return;
                }
                var comms = UnityEngine.Object.FindObjectOfType(type);
                if (comms == null)
                {
                    Log.Warn("Mic", "DissonanceComms instance not found — cannot mute.");
                    return;
                }

                var prop = HarmonyLib.Traverse.Create(comms).Property("IsMuted");
                if (prop.PropertyExists())
                    prop.SetValue(muted);
                else
                    HarmonyLib.Traverse.Create(comms).Field("IsMuted").SetValue(muted);
            }
            catch (System.Exception e)
            {
                Log.Error("Mic", $"failed to set mute: {e.Message}");
            }
        }
    }
}
