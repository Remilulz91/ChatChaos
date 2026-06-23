using System.Collections.Generic;
using ChatChaos.Networking;
using UnityEngine;

namespace ChatChaos.Events
{
    /// <summary>
    /// Reusable building blocks for event effects. These run ON THE HOST (an event's
    /// Apply() is only called on the host), and use the networker to mirror the effect
    /// to every player.
    /// </summary>
    public static class EventActions
    {
        /// <summary>
        /// Kills one random ALIVE player. Dead players (and empty slots) are excluded,
        /// so with 2 living players out of 3, one of the two living ones dies. Does
        /// nothing if no player is alive.
        /// </summary>
        public static void KillRandomAlivePlayer()
        {
            var sor = StartOfRound.Instance;
            if (sor == null || sor.allPlayerScripts == null) return;

            // Collect the indices of living, active players.
            var alive = new List<int>();
            for (int i = 0; i < sor.allPlayerScripts.Length; i++)
            {
                var p = sor.allPlayerScripts[i];
                if (p != null && p.isPlayerControlled && !p.isPlayerDead)
                    alive.Add(i);
            }

            if (alive.Count == 0)
            {
                Plugin.Log.LogInfo("random_death: no living player to kill.");
                return;
            }

            int index = alive[Random.Range(0, alive.Count)];
            Plugin.Log.LogInfo($"random_death: killing player index {index} " +
                               $"(of {alive.Count} alive).");

            var n = ChatChaosNetworker.Active;
            if (n != null)
            {
                n.KillPlayer(index);
            }
            else
            {
                // Fallback (no networker, e.g. very early/solo): kill locally if it's us.
                var t = sor.allPlayerScripts[index];
                if (t == sor.localPlayerController && !t.isPlayerDead)
                    t.KillPlayer(Vector3.zero, true, CauseOfDeath.Unknown, 0, default);
            }
        }

        /// <summary>
        /// Every LIVING player drops all the items they hold. Dead players are not
        /// affected. Networked: each machine drops its own player's items.
        /// </summary>
        public static void DropAllItemsFromLivingPlayers()
        {
            var n = ChatChaosNetworker.Active;
            if (n != null)
            {
                n.DropAllItems();
                return;
            }

            // Fallback (no networker, e.g. very early/solo): drop our own items.
            var sor = StartOfRound.Instance;
            var p = sor != null ? sor.localPlayerController : null;
            if (p != null && p.isPlayerControlled && !p.isPlayerDead)
                p.DropAllHeldItems(true, false);
        }

        /// <summary>
        /// Sets every LIVING player to 1 HP. Dead players are not affected. Networked:
        /// each machine damages its own player down to 1.
        /// </summary>
        public static void SetAllLivingPlayersToOneHp()
        {
            var n = ChatChaosNetworker.Active;
            if (n != null)
            {
                n.SetOneHp();
                return;
            }

            // Fallback (no networker, e.g. very early/solo): set our own HP to 1.
            var sor = StartOfRound.Instance;
            var p = sor != null ? sor.localPlayerController : null;
            if (p != null && p.isPlayerControlled && !p.isPlayerDead && p.health > 1)
                p.DamagePlayer(p.health - 1, true, true, CauseOfDeath.Unknown, 0, false, default);
        }

        /// <summary>
        /// Heals every LIVING player to full health (100 HP in vanilla). Dead players are
        /// not affected. Networked: each machine restores full health for everyone.
        /// </summary>
        public static void HealAllLivingPlayersToMax()
        {
            var n = ChatChaosNetworker.Active;
            if (n != null)
            {
                n.HealAll();
                return;
            }

            // Fallback (no networker, e.g. very early/solo): heal our own player.
            var sor = StartOfRound.Instance;
            var p = sor != null ? sor.localPlayerController : null;
            if (p != null && p.isPlayerControlled && !p.isPlayerDead)
            {
                p.health = 100;
                p.criticallyInjured = false;
                p.bleedingHeavily = false;
                if (HUDManager.Instance != null)
                    HUDManager.Instance.UpdateHealthUI(100, false);
            }
        }

        /// <summary>
        /// Changes the value of all scrap by <paramref name="percent"/> (negative = worth
        /// less, positive = worth more). Networked to every player.
        /// </summary>
        public static void ChangeScrapValue(int percent)
        {
            var n = ChatChaosNetworker.Active;
            if (n != null)
                n.ChangeScrapValues(percent);
            else
                Plugin.Log.LogWarning("ChatChaos: scrap value change skipped (networker not ready).");
        }

        /// <summary>
        /// Sets the battery of every battery-powered item to <paramref name="charge"/>
        /// (0 = empty, 1 = full). Flashlights, walkie-talkies, etc. Networked.
        /// </summary>
        public static void SetAllEquipmentBattery(float charge)
        {
            var n = ChatChaosNetworker.Active;
            if (n != null)
                n.SetBatteries(charge);
            else
                Plugin.Log.LogWarning("ChatChaos: battery change skipped (networker not ready).");
        }

        /// <summary>
        /// Unlocks/opens (unlock = true) or locks/closes (unlock = false) the level's doors.
        /// Adapts to the dungeon (classic DoorLock doors everywhere; big metal terminal doors
        /// only where they exist). Networked.
        /// </summary>
        public static void SetAllDoors(bool unlock)
        {
            var n = ChatChaosNetworker.Active;
            if (n != null)
                n.SetDoors(unlock);
            else
                Plugin.Log.LogWarning("ChatChaos: door change skipped (networker not ready).");
        }

        /// <summary>
        /// Teleports every living player back to the ship (inside the dungeon or outside).
        /// Networked.
        /// </summary>
        public static void TeleportAllLivingToShip()
        {
            var n = ChatChaosNetworker.Active;
            if (n != null)
                n.TeleportToShip();
            else
                Plugin.Log.LogWarning("ChatChaos: teleport skipped (networker not ready).");
        }

        /// <summary>Gives every player unlimited stamina for <paramref name="seconds"/>. Networked.</summary>
        public static void BoostStamina(float seconds)
        {
            var n = ChatChaosNetworker.Active;
            if (n != null)
                n.StartStaminaBoost(seconds);
            else
                Plugin.Log.LogWarning("ChatChaos: stamina boost skipped (networker not ready).");
        }

        /// <summary>Boosts every player's movement speed for <paramref name="seconds"/>. Networked.</summary>
        public static void BoostSpeed(float seconds)
        {
            var n = ChatChaosNetworker.Active;
            if (n != null)
                n.StartSpeedBoost(seconds);
            else
                Plugin.Log.LogWarning("ChatChaos: speed boost skipped (networker not ready).");
        }

        /// <summary>Detonates every landmine on the map. Networked.</summary>
        public static void ExplodeAllMines()
        {
            var n = ChatChaosNetworker.Active;
            if (n != null)
                n.ExplodeAllMines();
            else
                Plugin.Log.LogWarning("ChatChaos: explode mines skipped (networker not ready).");
        }

        /// <summary>
        /// Orders a random number of random store items (for free) so the dropship comes
        /// and delivers them. Uses the game's own buy RPC, so it's host-authoritative and
        /// synced. Runs on the host.
        /// </summary>
        public static void RandomDelivery()
        {
            var term = UnityEngine.Object.FindObjectOfType<Terminal>();
            if (term == null || term.buyableItemsList == null || term.buyableItemsList.Length == 0)
            {
                Plugin.Log.LogWarning("[ChatChaos][Delivery] terminal / item list not found.");
                return;
            }

            int catalog = term.buyableItemsList.Length;
            int count = UnityEngine.Random.Range(1, 9);   // 1..8 random items
            var items = new int[count];
            for (int i = 0; i < count; i++)
                items[i] = UnityEngine.Random.Range(0, catalog);

            int numInShip = term.numberOfItemsInDropship + count;
            try
            {
                // Pass the CURRENT credits as the "new" total -> no charge (free delivery).
                term.BuyItemsServerRpc(items, term.groupCredits, numInShip);
                Plugin.Log.LogInfo($"[ChatChaos][Delivery] ordered {count} random item(s) for free; dropship incoming.");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[ChatChaos][Delivery] failed: {e}");
            }
        }

        /// <summary>
        /// Replaces every INDOOR enemy with a Snare Flea ("larva") at its position. Outdoor
        /// enemies are left alone. Host-authoritative via the game's spawn/kill methods.
        /// </summary>
        public static void LarvaeInfestation()
        {
            var rm = RoundManager.Instance;
            if (rm == null || rm.SpawnedEnemies == null)
            {
                Plugin.Log.LogWarning("[ChatChaos][Larvae] RoundManager / enemy list not ready.");
                return;
            }

            var flea = FindSnareFleaType();
            if (flea == null)
            {
                Plugin.Log.LogWarning("[ChatChaos][Larvae] Snare Flea enemy type not found.");
                return;
            }

            // Snapshot the indoor enemies first (we modify the list while replacing).
            var toReplace = new List<EnemyAI>();
            foreach (var e in rm.SpawnedEnemies)
                if (e != null && !e.isEnemyDead && !e.isOutside && e.enemyType != flea)
                    toReplace.Add(e);

            int n = 0;
            foreach (var e in toReplace)
            {
                Vector3 pos = e.transform.position;
                float yRot = e.transform.eulerAngles.y;
                try
                {
                    e.KillEnemyOnOwnerClient(true);                 // remove the old enemy
                    rm.SpawnEnemyGameObject(pos, yRot, -1, flea);   // spawn a larva there
                    n++;
                }
                catch (System.Exception ex)
                {
                    Plugin.Log.LogError($"[ChatChaos][Larvae] replace failed: {ex.Message}");
                }
            }

            Plugin.Log.LogInfo($"[ChatChaos][Larvae] replaced {n} indoor enemy(ies) with snare fleas.");
        }

        /// <summary>Resets the in-game day clock to the morning start. Networked.</summary>
        public static void ResetToMorning()
        {
            var n = ChatChaosNetworker.Active;
            if (n != null)
                n.ResetDayTime();
            else
                Plugin.Log.LogWarning("ChatChaos: reset day time skipped (networker not ready).");
        }

        private static EnemyType? FindSnareFleaType()
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<EnemyType>())
            {
                if (t == null) continue;
                string name = (t.enemyName ?? "").ToLowerInvariant();
                if (name.Contains("centipede") || name.Contains("snare") || name.Contains("flea"))
                    return t;
            }
            return null;
        }

        /// <summary>
        /// Revives all dead players (teleported back to the ship). Living players are left
        /// where they are. Networked.
        /// </summary>
        public static void ReviveDeadTeam()
        {
            var n = ChatChaosNetworker.Active;
            if (n != null)
                n.ReviveTeam();
            else
                StartOfRound.Instance?.ReviveDeadPlayers();
        }

        /// <summary>Turns the facility (dungeon) power on (true) or off (false). Networked.</summary>
        public static void SetFacilityPower(bool on)
        {
            var n = ChatChaosNetworker.Active;
            if (n != null)
                n.SetFacilityPower(on);
            else
                Plugin.Log.LogWarning("ChatChaos: power change skipped (networker not ready).");
        }

        /// <summary>
        /// Picks a random weather from the CURRENT moon's possible weathers (prefers one
        /// different from the current weather) and applies it for everyone. Networked.
        /// </summary>
        public static void TriggerRandomWeather()
        {
            var level = StartOfRound.Instance != null ? StartOfRound.Instance.currentLevel : null;
            if (level == null)
            {
                Plugin.Log.LogWarning("[ChatChaos][Weather] no current moon — weather skipped.");
                return;
            }

            // Build the candidate list from the moon's possible weathers (+ clear).
            var candidates = new List<LevelWeatherType> { LevelWeatherType.None };
            if (level.randomWeathers != null)
                foreach (var rw in level.randomWeathers)
                    if (rw != null && !candidates.Contains(rw.weatherType))
                        candidates.Add(rw.weatherType);

            // Prefer a change: drop the current weather if there's an alternative.
            if (candidates.Count > 1)
                candidates.Remove(level.currentWeather);

            var chosen = candidates[Random.Range(0, candidates.Count)];
            Plugin.Log.LogInfo($"[ChatChaos][Weather] '{level.PlanetName}': chose {chosen} " +
                               $"from {candidates.Count} candidate(s).");

            var n = ChatChaosNetworker.Active;
            if (n != null)
                n.SetWeather((int)chosen);
            else
                Plugin.Log.LogWarning("[ChatChaos][Weather] networker not ready — weather skipped.");
        }
    }
}
