using ChatChaos.Core;

namespace ChatChaos.Events
{
    /// <summary>
    /// ============================================================
    ///  THIS IS THE FILE YOU EDIT TO ADD EVENTS.
    /// ============================================================
    ///
    /// Each call to Add(...) registers one option the chat can vote for. A poll
    /// then picks 3 of these at random. The last lambda is the in-game effect; it
    /// runs ON THE HOST when that option wins (the host syncs the rest).
    ///
    /// Template (copy/paste and fill in):
    ///
    ///     EventRegistry.Add(
    ///         "my_event_id",                 // unique id (a-z, _)
    ///         en: "English label",           // shown on screen + in chat (EN)
    ///         fr: "Libellé français",        // shown on screen + in chat (FR)
    ///         () =>
    ///         {
    ///             // Your game logic here. Runs on the host. Wrap risky calls in try/catch.
    ///             // Example of reaching the players:
    ///             //   var sor = StartOfRound.Instance;
    ///             //   foreach (var p in sor.allPlayerScripts)
    ///             //       if (p.isPlayerControlled && !p.isPlayerDead) { /* ... */ }
    ///         });
    ///
    /// The events below are PLACEHOLDERS so the system works end-to-end (panel,
    /// chat, voting, winner). They only log for now — replace their bodies with the
    /// real effects as you build them. The mod needs at least 3 events to fill a poll.
    /// </summary>
    public static class EventLibrary
    {
        public static void RegisterAll()
        {
            // --- Placeholder events (replace the bodies with real effects) ---

            EventRegistry.Add("random_death", "Random death", "Mort aléatoire", () =>
                EventActions.KillRandomAlivePlayer());

            EventRegistry.Add("drop_items", "Items dropped", "Objets lâchés", () =>
                EventActions.DropAllItemsFromLivingPlayers());

            EventRegistry.Add("one_hp", "1 HP", "1 HP", () =>
                Placeholder("one_hp"));

            EventRegistry.Add("recharge_gear", "Recharge equipment", "Recharge équipements", () =>
                Placeholder("recharge_gear"));

            EventRegistry.Add("power_outage", "Power outage", "Coupure de courant", () =>
                Placeholder("power_outage"));

            EventRegistry.Add("random_teleport", "Random teleport", "Téléportation aléatoire", () =>
                Placeholder("random_teleport"));

            // Add as many as you like below — copy the template from the comment above.
        }

        /// <summary>
        /// Temporary effect for a not-yet-implemented event: it just logs so you can
        /// see the full flow working. Delete these calls as you implement each event.
        /// </summary>
        private static void Placeholder(string id)
        {
            Plugin.Log.LogInfo($"[Event] '{id}' won the vote — placeholder effect (no game change yet).");
        }
    }
}
