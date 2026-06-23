using ChatChaos.UI;
using HarmonyLib;
using UnityEngine;

namespace ChatChaos.Core
{
    /// <summary>
    /// A tiny component, created once at startup, that:
    ///   - makes sure the poll panel (PollHud) exists,
    ///   - detects when the ship lands on / leaves a moon, and
    ///   - calls PollManager.Tick() every frame.
    ///
    /// We watch the ship state here (instead of patching a specific game method) so
    /// a game update that renames a method can't silently break the trigger; the
    /// ship-landed flag is read defensively via reflection.
    /// </summary>
    public class PollTicker : MonoBehaviour
    {
        public static PollTicker? Instance { get; private set; }

        private bool _prevLanded;

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("ChatChaos_Ticker");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            Instance = go.AddComponent<PollTicker>();

            PollHud.EnsureExists();
            BerserkHud.EnsureExists();
        }

        private void Update()
        {
            PollManager.Tick();
            PollManager.TickClientNotice();
            TimeFreeze.Tick();
            StaminaBoost.Tick();
            ShipLock.Tick();
            Berserk.Tick();
            TrackLanding();
        }

        private void TrackLanding()
        {
            var sor = StartOfRound.Instance;
            if (sor == null) { _prevLanded = false; return; }

            bool landed = ReadLanded(sor);
            if (landed && !_prevLanded)
            {
                _prevLanded = true;
                PollManager.OnLanded(IsCompanyMoon(sor), MoonName(sor));
            }
            else if (!landed && _prevLanded)
            {
                _prevLanded = false;
                PollManager.OnTookOff();
            }
        }

        private static bool ReadLanded(StartOfRound sor)
        {
            // "Landed on a moon" = the ship has landed AND is neither leaving nor back in
            // orbit. Reading shipIsLeaving makes the poll pause the instant the lever is
            // pulled, and combining the flags avoids false takeoffs from unrelated state
            // (e.g. the doors opening/closing). All read defensively via reflection.
            bool hasLanded = ReadBool(sor, "shipHasLanded");
            bool isLeaving = ReadBool(sor, "shipIsLeaving");
            bool inOrbit   = ReadBool(sor, "inShipPhase");
            return hasLanded && !isLeaving && !inOrbit;
        }

        private static bool ReadBool(StartOfRound sor, string field)
        {
            try
            {
                var v = Traverse.Create(sor).Field(field).GetValue();
                return v is bool b && b;
            }
            catch { return false; }
        }

        private static string MoonName(StartOfRound sor)
        {
            try { return sor.currentLevel?.PlanetName ?? ""; }
            catch { return ""; }
        }

        private static bool IsCompanyMoon(StartOfRound sor)
        {
            try
            {
                var level = sor.currentLevel;
                if (level == null) return false;
                if (level.levelID == 3) return true; // Gordion / Company building
                string name = (level.PlanetName ?? "").ToLowerInvariant();
                return name.Contains("company") || name.Contains("gordion");
            }
            catch { return false; }
        }
    }
}
