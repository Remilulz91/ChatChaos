using System;
using System.Collections.Generic;
using ChatChaos.Events;

namespace ChatChaos.Core
{
    /// <summary>
    /// The catalogue of every event the chat can vote for.
    ///
    /// HOW TO ADD AN EVENT (the only thing you usually touch):
    /// open src/Events/EventLibrary.cs and add a line like:
    ///
    ///     Add("random_death", en: "Random death", fr: "Mort aléatoire", () =>
    ///     {
    ///         // your game logic here — runs on the HOST when this option wins
    ///     });
    ///
    /// That's it. Each poll then picks 3 distinct events at random from this list.
    /// </summary>
    public static class EventRegistry
    {
        private static readonly List<ChatEvent> _events = new();
        private static readonly System.Random _rng = new();

        public static IReadOnlyList<ChatEvent> All => _events;
        public static int Count => _events.Count;

        /// <summary>Register a new event. Duplicate ids are ignored (last one wins a warning).</summary>
        public static void Add(string id, string en, string fr, Action apply)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Plugin.Log.LogWarning("EventRegistry.Add called with an empty id; skipped.");
                return;
            }
            if (_events.Exists(e => e.Id == id))
            {
                Plugin.Log.LogWarning($"EventRegistry: duplicate event id '{id}' ignored.");
                return;
            }
            _events.Add(new ChatEvent(id, en, fr, apply));
        }

        /// <summary>Called once at startup. Loads the placeholder/example events.</summary>
        public static void RegisterDefaults()
        {
            _events.Clear();
            EventLibrary.RegisterAll();
            Plugin.Log.LogInfo($"EventRegistry: {_events.Count} event(s) loaded.");
        }

        /// <summary>
        /// Pick <paramref name="count"/> DISTINCT events at random for a poll.
        /// If fewer than <paramref name="count"/> events exist, returns as many as
        /// possible (the poll will simply have fewer options).
        /// </summary>
        public static List<ChatEvent> PickRandom(int count)
        {
            var pool = new List<ChatEvent>(_events);
            var result = new List<ChatEvent>();
            count = Math.Min(count, pool.Count);
            for (int i = 0; i < count; i++)
            {
                int idx = _rng.Next(pool.Count);
                result.Add(pool[idx]);
                pool.RemoveAt(idx);
            }
            return result;
        }
    }
}
