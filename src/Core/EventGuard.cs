using System.Collections.Generic;
using UnityEngine;

namespace ChatChaos.Core
{
    /// <summary>
    /// Prevents non-stackable events from being proposed again while they are still active
    /// or pending. A locked event id is excluded from the poll options until it unlocks.
    ///
    /// Two kinds of lock:
    ///   - LockFor(id, seconds): time-based, auto-expires (for timed effects).
    ///   - Lock(id) / Unlock(id): manual (e.g. Double or Nothing stays locked until it
    ///     resolves at the Company).
    ///
    /// Host-side only (the host builds the poll options).
    /// </summary>
    public static class EventGuard
    {
        private static readonly Dictionary<string, float> _locked = new();   // id -> unlock time

        public static void Reset() => _locked.Clear();

        public static void Lock(string id) => _locked[id] = float.MaxValue;

        public static void LockFor(string id, float seconds) => _locked[id] = Time.time + seconds;

        public static void Unlock(string id) => _locked.Remove(id);

        public static bool IsLocked(string id)
        {
            if (!_locked.TryGetValue(id, out var until)) return false;
            if (Time.time >= until) { _locked.Remove(id); return false; }
            return true;
        }
    }
}
