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

            EventRegistry.Add("one_hp", "1 HP", "1 PV", () =>
                EventActions.SetAllLivingPlayersToOneHp());

            EventRegistry.Add("max_health", "Max health", "Santé max", () =>
                EventActions.HealAllLivingPlayersToMax());

            EventRegistry.Add("double_or_nothing", "Double or nothing", "Quitte ou double", () =>
                Core.DoubleOrNothing.Arm());

            // Dynamic events: the percentage (5-50) is rolled when the option is drawn, so
            // the chat sees the exact value, and that same value is applied if it wins.
            EventRegistry.AddDynamic("scrap_value_down", () =>
            {
                int pct = UnityEngine.Random.Range(5, 51);   // 5..50 inclusive
                return new Core.ChatEvent("scrap_value_down",
                    $"Scrap value -{pct}%", $"Valeur scrap -{pct}%",
                    () => EventActions.ChangeScrapValue(-pct));
            });

            EventRegistry.AddDynamic("scrap_value_up", () =>
            {
                int pct = UnityEngine.Random.Range(5, 51);   // 5..50 inclusive
                return new Core.ChatEvent("scrap_value_up",
                    $"Scrap value +{pct}%", $"Valeur scrap +{pct}%",
                    () => EventActions.ChangeScrapValue(pct));
            });

            EventRegistry.Add("recharge_gear", "Recharge equipment", "Recharge équipements", () =>
                EventActions.SetAllEquipmentBattery(1f));

            EventRegistry.Add("discharge_gear", "Discharge equipment", "Décharge équipements", () =>
                EventActions.SetAllEquipmentBattery(0f));

            EventRegistry.Add("unlock_doors", "Unlock doors", "Déverrouiller portes", () =>
                EventActions.SetAllDoors(true));

            EventRegistry.Add("lock_doors", "Lock doors", "Verrouiller portes", () =>
                EventActions.SetAllDoors(false));

            EventRegistry.Add("time_frozen", "Time frozen (1m)", "Temps figé (1m)", () =>
                Core.TimeFreeze.Freeze(60f));

            EventRegistry.Add("teleport_ship", "Teleport to ship", "Téléporter au vaisseau", () =>
                EventActions.TeleportAllLivingToShip());

            EventRegistry.Add("stamina_boost", "Stamina boost (1m)", "Stamina boostée (1m)", () =>
                EventActions.BoostStamina(60f));

            EventRegistry.Add("ship_locked", "Ship locked (30s)", "Vaisseau bloqué (30s)", () =>
                Core.ShipLock.Lock(30f));

            EventRegistry.Add("revive_team", "Team revive", "Résurrection équipe", () =>
                EventActions.ReviveDeadTeam());

            EventRegistry.Add("weather_random", "Random weather", "Météo aléatoire", () =>
                EventActions.TriggerRandomWeather());

            EventRegistry.Add("berserk", "Berserk (45s)", "Berserk (45s)", () =>
                Core.Berserk.Trigger());

            EventRegistry.Add("power_on", "Turn power on", "Allumer courant", () =>
                EventActions.SetFacilityPower(true));

            EventRegistry.Add("power_off", "Turn power off", "Eteindre courant", () =>
                EventActions.SetFacilityPower(false));

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
