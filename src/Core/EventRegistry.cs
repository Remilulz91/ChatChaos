using System;
using System.Collections.Generic;
using ChatChaos.Events;

namespace ChatChaos.Core
{
    /// <summary>
    /// The catalogue of every event the chat can vote for.
    ///
    /// Two kinds of events:
    ///   - STATIC: fixed label + effect. Register with Add(...).
    ///   - DYNAMIC: label and/or effect are rolled fresh each time the event is drawn
    ///     into a poll (e.g. "Scrap value -27%"). Register with AddDynamic(id, factory),
    ///     where the factory returns a freshly-built ChatEvent each call.
    ///
    /// Each poll picks 3 distinct events at random; dynamic ones are instantiated at that
    /// moment, so the rolled value shown on the panel is exactly what gets applied.
    /// </summary>
    public static class EventRegistry
    {
        private sealed class Def
        {
            public string Id = "";
            public Func<ChatEvent> Make = null!;
        }

        private static readonly List<Def> _defs = new();
        private static readonly System.Random _rng = new();

        public static int Count => _defs.Count;

        /// <summary>Register a STATIC event (fixed label + effect).</summary>
        public static void Add(string id, string en, string fr, Action apply)
        {
            if (!Validate(id)) return;
            var ev = new ChatEvent(id, en, fr, apply);
            _defs.Add(new Def { Id = id, Make = () => ev });
        }

        /// <summary>
        /// Register a DYNAMIC event. The factory is called each time this event is drawn
        /// into a poll and must return a freshly-built ChatEvent (roll your random values
        /// inside it so the label and the effect share the same value).
        /// </summary>
        public static void AddDynamic(string id, Func<ChatEvent> factory)
        {
            if (!Validate(id) || factory == null) return;
            _defs.Add(new Def { Id = id, Make = factory });
        }

        private static bool Validate(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Plugin.Log.LogWarning("EventRegistry: event with an empty id skipped.");
                return false;
            }
            if (_defs.Exists(d => d.Id == id))
            {
                Plugin.Log.LogWarning($"EventRegistry: duplicate event id '{id}' ignored.");
                return false;
            }
            return true;
        }

        /// <summary>Called once at startup. Loads the events from EventLibrary.</summary>
        public static void RegisterDefaults()
        {
            _defs.Clear();
            EventLibrary.RegisterAll();
            Plugin.Log.LogInfo($"EventRegistry: {_defs.Count} event(s) loaded.");
        }

        /// <summary>
        /// Pick <paramref name="count"/> DISTINCT events at random for a poll, building a
        /// fresh instance of each (so dynamic events roll their value now). Returns fewer
        /// if not enough events exist.
        /// </summary>
        public static List<ChatEvent> PickRandom(int count)
        {
            var pool = new List<Def>(_defs);
            var result = new List<ChatEvent>();
            count = Math.Min(count, pool.Count);
            for (int i = 0; i < count; i++)
            {
                int idx = _rng.Next(pool.Count);
                result.Add(pool[idx].Make());
                pool.RemoveAt(idx);
            }
            return result;
        }
    }
}
