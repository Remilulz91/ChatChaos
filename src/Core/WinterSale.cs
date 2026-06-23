using ChatChaos.Logging;
using ChatChaos.Networking;
using UnityEngine;

namespace ChatChaos.Core
{
    /// <summary>
    /// "Winter sale": forces a random discount (30-90% off) on every store item for a
    /// duration, then restores the original sales. The discount is generated from a shared
    /// seed so every machine computes the exact same prices (so the host charges the
    /// discounted price too). Host owns the timer.
    /// </summary>
    public static class WinterSale
    {
        private const int MinOff = 30;   // minimum discount %
        private const int MaxOff = 90;   // maximum discount %

        // Host timing.
        private static bool _active;
        private static float _endTime;

        // Per-machine: the original sales to restore.
        private static int[]? _saved;
        private static bool _applied;

        public static void Reset()
        {
            _active = false;
            _endTime = 0f;
            if (_applied) RestoreLocal();
        }

        /// <summary>Host: start the sale for <paramref name="seconds"/>.</summary>
        public static void Trigger(float seconds)
        {
            if (_active) return;   // one sale at a time
            var n = ChatChaosNetworker.Active;
            if (n == null)
            {
                Log.Warn("Sale", "winter sale skipped (networker not ready).");
                return;
            }
            int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            _active = true;
            _endTime = Time.time + seconds;
            n.SetWinterSale(seed);
            Log.Info("Sale", $"winter sale started for {seconds:0}s (seed {seed}).");
        }

        /// <summary>Host: end the sale when the timer runs out.</summary>
        public static void Tick()
        {
            if (!_active) return;
            if (Time.time >= _endTime)
            {
                _active = false;
                ChatChaosNetworker.Active?.EndWinterSale();
            }
        }

        /// <summary>Per-machine: apply the discounts from the shared seed.</summary>
        public static void ApplyLocal(int seed)
        {
            var term = UnityEngine.Object.FindObjectOfType<Terminal>();
            if (term == null || term.itemSalesPercentages == null) return;

            var sales = term.itemSalesPercentages;
            if (!_applied)
            {
                _saved = (int[])sales.Clone();   // remember the original sales once
                _applied = true;
            }

            var rng = new System.Random(seed);
            for (int i = 0; i < sales.Length; i++)
            {
                int off = rng.Next(MinOff, MaxOff + 1);   // 30..90 % off
                sales[i] = 100 - off;                      // multiplier 10..70 (% of base price)
            }
            Log.Info("Sale", $"applied winter sale to {sales.Length} item(s).");
        }

        /// <summary>Per-machine: restore the original sales.</summary>
        public static void RestoreLocal()
        {
            var term = UnityEngine.Object.FindObjectOfType<Terminal>();
            if (term != null && _saved != null && term.itemSalesPercentages != null &&
                term.itemSalesPercentages.Length == _saved.Length)
            {
                System.Array.Copy(_saved, term.itemSalesPercentages, _saved.Length);
                Log.Info("Sale", "winter sale ended; original prices restored.");
            }
            _applied = false;
            _saved = null;
        }
    }
}
