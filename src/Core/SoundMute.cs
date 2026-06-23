using ChatChaos.Logging;
using UnityEngine;

namespace ChatChaos.Core
{
    /// <summary>
    /// "Mute the sound": silences ALL game audio output on the HOST (game sounds AND other
    /// players' voices) for a duration, then restores it. Done with AudioListener.volume,
    /// so it affects everything that plays through Unity audio. Host-local (the event runs
    /// on the host), so no networking is needed.
    /// </summary>
    public static class SoundMute
    {
        private static float _endTime;
        private static bool _active;
        private static float _savedVolume = 1f;

        public static void Reset()
        {
            _endTime = 0f;
            if (_active)
            {
                AudioListener.volume = _savedVolume;
                _active = false;
            }
        }

        /// <summary>Mute all game audio on this (host) machine for <paramref name="seconds"/>.</summary>
        public static void Mute(float seconds)
        {
            if (!_active) _savedVolume = AudioListener.volume;   // capture the real volume once
            _endTime = Time.time + seconds;
            _active = true;
            AudioListener.volume = 0f;
            Log.Info("Sound", $"host game sound muted for {seconds:0}s.");
        }

        /// <summary>Called every frame on the host; keeps it muted, then restores at the end.</summary>
        public static void Tick()
        {
            if (!_active) return;

            if (Time.time < _endTime)
            {
                if (AudioListener.volume != 0f) AudioListener.volume = 0f;   // keep muted
            }
            else
            {
                AudioListener.volume = _savedVolume;
                _active = false;
                Log.Info("Sound", "host game sound restored.");
            }
        }
    }
}
