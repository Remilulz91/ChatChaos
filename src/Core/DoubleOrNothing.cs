using ChatChaos.Localization;
using ChatChaos.Networking;
using ChatChaos.UI;
using UnityEngine;

namespace ChatChaos.Core
{
    /// <summary>
    /// "Double or Nothing" — a poll option that, when it WINS, ARMS a gamble. The gamble
    /// stays armed (across moons and days) until the ship next visits the Company:
    ///   - arriving at the Company shows a warning message,
    ///   - leaving the Company (the lever) doubles or halves (50/50) ALL the terminal's
    ///     group credits, with a WON / LOST message, then disarms.
    ///
    /// All host-driven; the message and the credit change are mirrored to every player.
    /// Because polls never run at the Company, these messages never overlap the poll panel.
    /// </summary>
    public static class DoubleOrNothing
    {
        private static bool _armed;
        private static bool _atCompany;

        /// <summary>Reset at game start.</summary>
        public static void Reset()
        {
            _armed = false;
            _atCompany = false;
            EventGuard.Unlock("double_or_nothing");
        }

        /// <summary>Called when the poll option wins (host only).</summary>
        public static void Arm()
        {
            _armed = true;
            EventGuard.Lock("double_or_nothing");   // not re-proposable until it resolves
            Plugin.Log.LogInfo("DoubleOrNothing: armed — resolves at the next Company visit.");
        }

        /// <summary>Host: ship landed (isCompany = the safe Company building).</summary>
        public static void OnLanded(bool isCompany)
        {
            _atCompany = isCompany;
            if (isCompany && _armed)
                ShowTip(Loc.Get("qod.header"), Loc.Get("qod.armed"));
        }

        /// <summary>Host: ship took off. Resolve if leaving the Company while armed.</summary>
        public static void OnTookOff()
        {
            if (!_armed || !_atCompany)
            {
                _atCompany = false;
                return;
            }
            _armed = false;
            _atCompany = false;
            Resolve();
        }

        private static void Resolve()
        {
            EventGuard.Unlock("double_or_nothing");   // resolved -> can be proposed again

            var terminal = UnityEngine.Object.FindObjectOfType<Terminal>();
            if (terminal == null)
            {
                Plugin.Log.LogWarning("DoubleOrNothing: terminal not found — gamble skipped.");
                return;
            }

            int credits = terminal.groupCredits;
            bool win = Random.value < 0.5f;
            int newCredits = win ? credits * 2 : credits / 2;
            int delta = Mathf.Abs(newCredits - credits);

            var n = ChatChaosNetworker.Active;
            if (n != null) n.SetGroupCredits(newCredits);
            else terminal.groupCredits = newCredits;

            Plugin.Log.LogInfo($"DoubleOrNothing: {(win ? "WON" : "LOST")} — credits {credits} -> {newCredits}.");

            string body = win ? Loc.Format("qod.win", delta) : Loc.Format("qod.lose", delta);
            ShowTip(Loc.Get("qod.header"), body);
        }

        private static void ShowTip(string header, string body)
        {
            var n = ChatChaosNetworker.Active;
            if (n != null) n.BroadcastTip(header, body);
            else GameTips.Show(header, body);
        }
    }
}
