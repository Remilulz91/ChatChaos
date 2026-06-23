using System.Collections.Generic;
using ChatChaos.Localization;
using UnityEngine;

namespace ChatChaos.Core
{
    /// <summary>
    /// Tracks the time-limited effects that are currently active, so the HUD can show a
    /// countdown for each ("Time frozen 42s", "Berserk 30s", ...). Each timed event
    /// registers its timer here (broadcast to every machine), and they expire on their own.
    /// </summary>
    public static class EffectTimers
    {
        private sealed class Entry
        {
            public string Id = "";
            public string LabelKey = "";
            public float EndTime;
        }

        private static readonly List<Entry> _entries = new();

        public static void Reset() => _entries.Clear();

        /// <summary>Start/refresh a countdown for <paramref name="id"/> (per-machine).</summary>
        public static void Start(string id, string labelKey, float seconds)
        {
            var e = _entries.Find(x => x.Id == id);
            if (e == null)
            {
                e = new Entry { Id = id };
                _entries.Add(e);
            }
            e.LabelKey = labelKey;
            e.EndTime = Time.time + seconds;
        }

        public static void Stop(string id) => _entries.RemoveAll(x => x.Id == id);

        /// <summary>Drop expired timers (call every frame).</summary>
        public static void Tick()
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
                if (Time.time >= _entries[i].EndTime) _entries.RemoveAt(i);
        }

        /// <summary>For the HUD: localized label + whole seconds remaining, per active effect.</summary>
        public static List<(string label, int secs)> GetActive()
        {
            var list = new List<(string, int)>();
            foreach (var e in _entries)
            {
                int s = Mathf.Max(0, Mathf.CeilToInt(e.EndTime - Time.time));
                list.Add((Loc.Get(e.LabelKey), s));
            }
            return list;
        }
    }
}
