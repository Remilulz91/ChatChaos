using System;
using ChatChaos.UI;
using Unity.Netcode;
using UnityEngine;

namespace ChatChaos.Networking
{
    /// <summary>
    /// The mod's shared network component (one instance, spawned by the host).
    ///
    /// Same robustness pattern as the rest of the project: the host applies every
    /// UI change DIRECTLY (locally) and only uses ClientRpc to mirror it to REMOTE
    /// clients. RPC calls are wrapped so a failure (e.g. an incompletely
    /// netcode-patched build) is non-fatal — solo/host play always works and
    /// multiplayer sync is best-effort.
    /// </summary>
    public class ChatChaosNetworker : NetworkBehaviour
    {
        public static ChatChaosNetworker? Instance { get; private set; }
        private static bool _warnedRpc;

        /// <summary>The live networker, re-acquired if the cached one was destroyed.</summary>
        public static ChatChaosNetworker? Active
        {
            get
            {
                if (Instance != null) return Instance;
                Instance = UnityEngine.Object.FindObjectOfType<ChatChaosNetworker>();
                return Instance;
            }
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            base.OnNetworkSpawn();
            Plugin.Log.LogInfo("ChatChaosNetworker ready (network active).");
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            base.OnNetworkDespawn();
        }

        private static void Safe(Action rpc)
        {
            try { rpc(); }
            catch (Exception e)
            {
                if (_warnedRpc) return;
                _warnedRpc = true;
                Plugin.Log.LogWarning(
                    "Networking note: an RPC could not be sent. This is harmless in solo " +
                    "and if the build isn't fully netcode-patched; multiplayer sync may be " +
                    $"limited. ({e.Message})");
            }
        }

        // ===================== POLL UI SYNC (host -> clients) =====================

        public void BroadcastStart(string l0, string l1, string l2, float duration)
        {
            if (!IsServer) return;
            PollHud.Instance?.ShowPoll(l0, l1, l2, duration); // host renders locally
            Safe(() => StartPollClientRpc(l0, l1, l2, duration));
        }

        public void BroadcastCounts(int c0, int c1, int c2)
        {
            if (!IsServer) return;
            PollHud.Instance?.UpdateCounts(c0, c1, c2);
            Safe(() => CountsClientRpc(c0, c1, c2));
        }

        public void BroadcastEnd(int winnerIndex, int winnerCount)
        {
            if (!IsServer) return;
            PollHud.Instance?.ShowResult(winnerIndex, winnerCount);
            Safe(() => EndPollClientRpc(winnerIndex, winnerCount));
        }

        public void BroadcastHide()
        {
            if (!IsServer) return;
            PollHud.Instance?.Hide();
            Safe(() => HideClientRpc());
        }

        public void BroadcastTip(string header, string body)
        {
            if (!IsServer) return;
            GameTips.Show(header, body);
            Safe(() => TipClientRpc(header, body));
        }

        public void BroadcastPause()
        {
            if (!IsServer) return;
            PollHud.Instance?.Pause();
            Safe(() => PauseClientRpc());
        }

        // ===================== EVENT EFFECTS (host -> clients) =====================

        /// <summary>
        /// Kills the player at <paramref name="index"/> in StartOfRound.allPlayerScripts.
        /// Each machine kills only its OWN player (the owner), so the game syncs the death
        /// naturally. The host handles its own player locally and mirrors to clients.
        /// </summary>
        public void KillPlayer(int index)
        {
            if (!IsServer) return;
            KillIfLocalTarget(index);                 // host's own player, if it's the target
            Safe(() => KillPlayerClientRpc(index));    // every other player
        }

        [ClientRpc]
        private void KillPlayerClientRpc(int index)
        {
            if (IsServer) return;   // host already handled its own player
            KillIfLocalTarget(index);
        }

        private static void KillIfLocalTarget(int index)
        {
            var sor = StartOfRound.Instance;
            if (sor == null || sor.allPlayerScripts == null) return;
            if (index < 0 || index >= sor.allPlayerScripts.Length) return;

            var target = sor.allPlayerScripts[index];
            if (target == null) return;
            if (target != sor.localPlayerController) return;   // only kill your OWN player
            if (target.isPlayerDead) return;

            target.KillPlayer(Vector3.zero, true, CauseOfDeath.Unknown, 0, default);
        }

        /// <summary>
        /// Makes every LIVING player drop all the items they hold. Each machine drops its
        /// own player's items (the owner), so the drops sync correctly.
        /// </summary>
        public void DropAllItems()
        {
            if (!IsServer) return;
            DropLocalPlayerItems();                       // host's own player
            Safe(() => DropAllItemsClientRpc());          // every other player
        }

        [ClientRpc]
        private void DropAllItemsClientRpc()
        {
            if (IsServer) return;   // host already dropped its own
            DropLocalPlayerItems();
        }

        private static void DropLocalPlayerItems()
        {
            var sor = StartOfRound.Instance;
            if (sor == null) return;

            var p = sor.localPlayerController;
            if (p == null || !p.isPlayerControlled || p.isPlayerDead) return;

            p.DropAllHeldItems(true, false);
        }

        /// <summary>
        /// Sets every LIVING player to 1 HP. Each machine damages its own player (the
        /// owner) down to 1, and the game's damage RPC syncs the health bar to everyone.
        /// </summary>
        public void SetOneHp()
        {
            if (!IsServer) return;
            SetLocalPlayerOneHp();                     // host's own player
            Safe(() => SetOneHpClientRpc());           // every other player
        }

        [ClientRpc]
        private void SetOneHpClientRpc()
        {
            if (IsServer) return;   // host already handled its own
            SetLocalPlayerOneHp();
        }

        private static void SetLocalPlayerOneHp()
        {
            var sor = StartOfRound.Instance;
            if (sor == null) return;

            var p = sor.localPlayerController;
            if (p == null || !p.isPlayerControlled || p.isPlayerDead) return;

            int damage = p.health - 1;
            if (damage <= 0) return;   // already at 1 HP (or less)

            p.DamagePlayer(damage, true, true, CauseOfDeath.Unknown, 0, false, default);
        }

        private const int MaxHp = 100;   // vanilla max health

        /// <summary>
        /// Heals every LIVING player to full health. The game has no synced heal, so each
        /// machine sets every living player's health to max (keeping all copies in sync),
        /// and refreshes the HUD + clears the injured state for its own player.
        /// </summary>
        public void HealAll()
        {
            if (!IsServer) return;
            HealAllPlayersLocal();                     // host machine
            Safe(() => HealAllClientRpc());            // every other machine
        }

        [ClientRpc]
        private void HealAllClientRpc()
        {
            if (IsServer) return;   // host already healed on its machine
            HealAllPlayersLocal();
        }

        /// <summary>Sets the terminal's group credits on every machine (for Double or Nothing).</summary>
        public void SetGroupCredits(int credits)
        {
            if (!IsServer) return;
            ApplyGroupCreditsLocal(credits);
            Safe(() => SetGroupCreditsClientRpc(credits));
        }

        [ClientRpc]
        private void SetGroupCreditsClientRpc(int credits)
        {
            if (IsServer) return;
            ApplyGroupCreditsLocal(credits);
        }

        private static void ApplyGroupCreditsLocal(int credits)
        {
            var terminal = UnityEngine.Object.FindObjectOfType<Terminal>();
            if (terminal == null) return;
            terminal.groupCredits = credits;
        }

        /// <summary>
        /// Changes the value of every scrap item by <paramref name="percent"/> (e.g. -25 or
        /// +30). Each machine applies the same percentage to its own copies, so values stay
        /// in sync without sending every item.
        /// </summary>
        public void ChangeScrapValues(int percent)
        {
            if (!IsServer) return;
            ApplyScrapValueChangeLocal(percent);
            Safe(() => ChangeScrapValuesClientRpc(percent));
        }

        [ClientRpc]
        private void ChangeScrapValuesClientRpc(int percent)
        {
            if (IsServer) return;
            ApplyScrapValueChangeLocal(percent);
        }

        private static void ApplyScrapValueChangeLocal(int percent)
        {
            float factor = 1f + percent / 100f;
            var items = UnityEngine.Object.FindObjectsOfType<GrabbableObject>();
            int changed = 0;
            foreach (var g in items)
            {
                if (g == null || g.itemProperties == null || !g.itemProperties.isScrap) continue;

                int newVal = Mathf.Max(0, Mathf.RoundToInt(g.scrapValue * factor));
                g.scrapValue = newVal;

                // Refresh the scan display ("Value: $X") if present.
                var node = g.GetComponentInChildren<ScanNodeProperties>();
                if (node != null) node.subText = $"Value: ${newVal}";
                changed++;
            }
            Plugin.Log.LogInfo($"ChatChaos: scrap value {(percent >= 0 ? "+" : "")}{percent}% applied to {changed} item(s).");
        }

        /// <summary>
        /// Sets the battery charge (0..1) of every battery-powered item (flashlight, walkie,
        /// etc.). Applied identically on every machine so the charge stays in sync.
        /// </summary>
        public void SetBatteries(float charge)
        {
            if (!IsServer) return;
            ApplyBatteryChargeLocal(charge);
            Safe(() => SetBatteriesClientRpc(charge));
        }

        [ClientRpc]
        private void SetBatteriesClientRpc(float charge)
        {
            if (IsServer) return;
            ApplyBatteryChargeLocal(charge);
        }

        private static void ApplyBatteryChargeLocal(float charge)
        {
            charge = Mathf.Clamp01(charge);
            var items = UnityEngine.Object.FindObjectsOfType<GrabbableObject>();
            int changed = 0;
            foreach (var g in items)
            {
                if (g == null || g.itemProperties == null || !g.itemProperties.requiresBattery) continue;
                if (g.insertedBattery == null) continue;

                g.insertedBattery.charge = charge;
                g.insertedBattery.empty = charge <= 0f;
                changed++;
            }
            Plugin.Log.LogInfo($"ChatChaos: set battery to {Mathf.RoundToInt(charge * 100)}% on {changed} item(s).");
        }

        /// <summary>
        /// Unlocks/opens (unlock=true) or locks/closes (unlock=false) the level's doors.
        /// Big metal terminal doors are handled host-authoritatively via the game's own
        /// RPC; classic DoorLock doors are mirrored to every player. Adapts to the dungeon
        /// (acts only on the door types that exist).
        /// </summary>
        public void SetDoors(bool unlock)
        {
            if (!IsServer) return;
            ApplyBigDoorsHost(unlock);            // big terminal doors (sync themselves)
            ApplyClassicDoorsLocal(unlock);       // host's own classic doors
            Safe(() => ClassicDoorsClientRpc(unlock));
        }

        [ClientRpc]
        private void ClassicDoorsClientRpc(bool unlock)
        {
            if (IsServer) return;
            ApplyClassicDoorsLocal(unlock);
        }

        private static void ApplyBigDoorsHost(bool unlock)
        {
            int n = 0;
            foreach (var tao in UnityEngine.Object.FindObjectsOfType<TerminalAccessibleObject>())
            {
                if (tao == null || !tao.isBigDoor) continue;
                tao.SetDoorOpenServerRpc(unlock);   // unlock -> open, lock -> close
                n++;
            }
            if (n > 0) Plugin.Log.LogInfo($"ChatChaos: {(unlock ? "opened" : "closed")} {n} big door(s).");
        }

        private static void ApplyClassicDoorsLocal(bool unlock)
        {
            int n = 0;
            foreach (var door in UnityEngine.Object.FindObjectsOfType<DoorLock>())
            {
                if (door == null) continue;
                if (unlock)
                {
                    if (door.isLocked) { door.UnlockDoor(); n++; }   // no key needed; player opens by hand
                }
                else
                {
                    // Lock only doors that aren't already open (and aren't already locked).
                    // LockDoor() sets the locked state AND shows the padlock (same method the
                    // game uses), applied per-machine so everyone sees the padlock.
                    if (!door.isDoorOpened && !door.isLocked) { door.LockDoor(); n++; }
                }
            }
            if (n > 0) Plugin.Log.LogInfo($"ChatChaos: {(unlock ? "unlocked" : "locked")} {n} classic door(s).");
        }

        private static float _savedTimeMultiplier = 1f;

        /// <summary>
        /// Freezes (frozen=true) or resumes (frozen=false) the in-game day clock on every
        /// machine, by zeroing / restoring TimeOfDay.globalTimeSpeedMultiplier.
        /// </summary>
        public void SetTimeFrozen(bool frozen)
        {
            if (!IsServer) return;
            ApplyTimeFrozenLocal(frozen);
            Safe(() => SetTimeFrozenClientRpc(frozen));
        }

        [ClientRpc]
        private void SetTimeFrozenClientRpc(bool frozen)
        {
            if (IsServer) return;
            ApplyTimeFrozenLocal(frozen);
        }

        private static void ApplyTimeFrozenLocal(bool frozen)
        {
            var tod = TimeOfDay.Instance;
            if (tod == null) return;

            if (frozen)
            {
                // Capture the real running speed (avoid capturing 0 on a double-freeze).
                if (tod.globalTimeSpeedMultiplier > 0f)
                    _savedTimeMultiplier = tod.globalTimeSpeedMultiplier;
                tod.globalTimeSpeedMultiplier = 0f;
                Plugin.Log.LogInfo("ChatChaos: in-game time frozen.");
            }
            else
            {
                tod.globalTimeSpeedMultiplier = _savedTimeMultiplier > 0f ? _savedTimeMultiplier : 1f;
                Plugin.Log.LogInfo("ChatChaos: in-game time resumed.");
            }
        }

        /// <summary>
        /// Teleports every LIVING player back to the ship (from inside the dungeon or
        /// outside). Each machine teleports its own player (the owner), so the position
        /// and room state sync naturally.
        /// </summary>
        public void TeleportToShip()
        {
            if (!IsServer) return;
            TeleportLocalToShip();
            Safe(() => TeleportToShipClientRpc());
        }

        [ClientRpc]
        private void TeleportToShipClientRpc()
        {
            if (IsServer) return;
            TeleportLocalToShip();
        }

        private static void TeleportLocalToShip()
        {
            var sor = StartOfRound.Instance;
            if (sor == null) return;

            var p = sor.localPlayerController;
            if (p == null || !p.isPlayerControlled || p.isPlayerDead) return;

            var spawns = sor.playerSpawnPositions;
            if (spawns == null || spawns.Length == 0) return;

            int idx = Array.IndexOf(sor.allPlayerScripts, p);
            int i = Mathf.Clamp(idx < 0 ? 0 : idx, 0, spawns.Length - 1);
            if (spawns[i] == null) return;

            p.TeleportPlayer(spawns[i].position);
            p.isInElevator = true;
            p.isInHangarShipRoom = true;
            p.isInsideFactory = false;
        }

        /// <summary>Gives every player unlimited stamina for <paramref name="seconds"/>.</summary>
        public void StartStaminaBoost(float seconds)
        {
            if (!IsServer) return;
            Core.StaminaBoost.Activate(seconds);   // host's own player
            Safe(() => StaminaBoostClientRpc(seconds));
        }

        [ClientRpc]
        private void StaminaBoostClientRpc(float seconds)
        {
            if (IsServer) return;
            Core.StaminaBoost.Activate(seconds);
        }

        /// <summary>Boosts every player's movement speed for <paramref name="seconds"/>.</summary>
        public void StartSpeedBoost(float seconds)
        {
            if (!IsServer) return;
            Core.SpeedBoost.Activate(seconds);
            Safe(() => SpeedBoostClientRpc(seconds));
        }

        [ClientRpc]
        private void SpeedBoostClientRpc(float seconds)
        {
            if (IsServer) return;
            Core.SpeedBoost.Activate(seconds);
        }

        /// <summary>
        /// Locks (closes the hangar door + blocks the lever) or unlocks the ship on every
        /// machine. The door/lever interactables are local, so each machine applies it.
        /// </summary>
        public void SetShipLocked(bool locked)
        {
            if (!IsServer) return;
            ApplyShipLockedLocal(locked);
            Safe(() => SetShipLockedClientRpc(locked));
        }

        [ClientRpc]
        private void SetShipLockedClientRpc(bool locked)
        {
            if (IsServer) return;
            ApplyShipLockedLocal(locked);
        }

        private static void ApplyShipLockedLocal(bool locked)
        {
            // Lever: block / unblock takeoff.
            var lever = UnityEngine.Object.FindObjectOfType<StartMatchLever>();
            if (lever != null && lever.triggerScript != null)
                lever.triggerScript.interactable = !locked;

            // Hangar door: close (locked) / open (unlocked) + block its buttons.
            var door = UnityEngine.Object.FindObjectOfType<HangarShipDoor>();
            if (door != null)
            {
                if (door.shipDoorsAnimator != null)
                    door.shipDoorsAnimator.SetBool("Closed", locked);

                // Best-effort: disable the door buttons so it can't be reopened. The field
                // name varies between game versions, so we look it up defensively.
                foreach (var fieldName in new[] { "doorButtons", "buttons", "interactTriggers", "triggers" })
                {
                    try
                    {
                        var arr = HarmonyLib.Traverse.Create(door).Field(fieldName).GetValue() as InteractTrigger[];
                        if (arr != null)
                            foreach (var t in arr) if (t != null) t.interactable = !locked;
                    }
                    catch { /* field not present in this version */ }
                }
            }

            Plugin.Log.LogInfo($"ChatChaos: ship {(locked ? "locked (door closed, lever blocked)" : "unlocked")}.");
        }

        /// <summary>
        /// Revives all DEAD players and teleports them back to the ship (the game's own
        /// ReviveDeadPlayers does exactly this and leaves living players where they are).
        /// Run on every machine, matching how the game itself revives on the ship-leave.
        /// </summary>
        public void ReviveTeam()
        {
            if (!IsServer) return;
            ApplyReviveLocal();
            Safe(() => ReviveTeamClientRpc());
        }

        [ClientRpc]
        private void ReviveTeamClientRpc()
        {
            if (IsServer) return;
            ApplyReviveLocal();
        }

        private static void ApplyReviveLocal()
        {
            var sor = StartOfRound.Instance;
            if (sor == null) return;
            sor.ReviveDeadPlayers();
            Plugin.Log.LogInfo("ChatChaos: revived dead players (teleported to the ship).");
        }

        /// <summary>
        /// Turns the facility power on/off on every machine. The exact method name varies
        /// between game versions, so we call it by reflection trying several candidates;
        /// this guarantees the build never breaks on a wrong name.
        /// </summary>
        public void SetFacilityPower(bool on)
        {
            if (!IsServer) return;
            ApplyPowerLocal(on);
            Safe(() => SetFacilityPowerClientRpc(on));
        }

        [ClientRpc]
        private void SetFacilityPowerClientRpc(bool on)
        {
            if (IsServer) return;
            ApplyPowerLocal(on);
        }

        private static void ApplyPowerLocal(bool on)
        {
            var box = UnityEngine.Object.FindObjectOfType<BreakerBox>();
            var rm = RoundManager.Instance;

            bool done =
                TryInvoke(box, "SwitchBreaker", on) ||
                TryInvoke(rm, "SwitchPower", on) ||
                TryInvoke(rm, on ? "PowerSwitchOn" : "PowerSwitchOff") ||
                TryInvoke(rm, on ? "TurnOnAllLights" : "TurnOffAllLights");

            Plugin.Log.LogInfo($"ChatChaos: facility power {(on ? "on" : "off")} -> " +
                               (done ? "applied" : "NO matching method found (tell the dev)."));
        }

        private static bool TryInvoke(object target, string method, params object[] args)
        {
            if (target == null) return false;
            try
            {
                var m = HarmonyLib.Traverse.Create(target).Method(method, args);
                if (m.MethodExists())
                {
                    m.GetValue();
                    return true;
                }
            }
            catch { /* try the next candidate */ }
            return false;
        }

        /// <summary>
        /// Applies a weather type (by its LevelWeatherType int) on every machine: sets the
        /// level's weather and toggles the matching visual effect. Effects are toggled by
        /// reflection so the build can't break on a field-name difference.
        /// </summary>
        public void SetWeather(int weather)
        {
            if (!IsServer) return;
            ApplyWeatherLocal(weather);
            Safe(() => SetWeatherClientRpc(weather));
        }

        [ClientRpc]
        private void SetWeatherClientRpc(int weather)
        {
            if (IsServer) return;
            ApplyWeatherLocal(weather);
        }

        private static void ApplyWeatherLocal(int weather)
        {
            var w = (LevelWeatherType)weather;

            var sor = StartOfRound.Instance;
            if (sor != null && sor.currentLevel != null)
                sor.currentLevel.currentWeather = w;

            var tod = TimeOfDay.Instance;
            if (tod != null)
            {
                try { HarmonyLib.Traverse.Create(tod).Field("currentLevelWeather").SetValue(w); }
                catch { /* field name differs; not fatal */ }
                ToggleWeatherEffects(tod, weather);
            }

            Plugin.Log.LogInfo($"[ChatChaos][Weather] applied '{w}'.");
        }

        /// <summary>Shows the "RECEIVING SIGNAL / GO BERSERK" overlay on every machine.</summary>
        public void ShowBerserkSignal()
        {
            if (!IsServer) return;
            BerserkHud.Instance?.ShowSignal();
            Safe(() => ShowBerserkSignalClientRpc());
        }

        [ClientRpc]
        private void ShowBerserkSignalClientRpc()
        {
            if (IsServer) return;
            BerserkHud.Instance?.ShowSignal();
        }

        /// <summary>Sets the invincible Berserk player (index, -1 = none) on every machine.</summary>
        public void SetBerserkPlayer(int index)
        {
            if (!IsServer) return;
            Core.Berserk.SetActivePlayer(index);
            Safe(() => SetBerserkPlayerClientRpc(index));
        }

        [ClientRpc]
        private void SetBerserkPlayerClientRpc(int index)
        {
            if (IsServer) return;
            Core.Berserk.SetActivePlayer(index);
        }

        /// <summary>Resets the in-game day clock back to the morning start on every machine.</summary>
        public void ResetDayTime()
        {
            if (!IsServer) return;
            ApplyResetDayTimeLocal();
            Safe(() => ResetDayTimeClientRpc());
        }

        [ClientRpc]
        private void ResetDayTimeClientRpc()
        {
            if (IsServer) return;
            ApplyResetDayTimeLocal();
        }

        private static void ApplyResetDayTimeLocal()
        {
            var tod = TimeOfDay.Instance;
            if (tod == null) return;
            tod.currentDayTime = 0f;   // 0 = the default landing/morning time
            Plugin.Log.LogInfo("[ChatChaos][Time] day clock reset to the morning start.");
        }

        private static GrabbableObject? _hostBerserkShotgun;

        /// <summary>Host: spawn a shotgun and give it to player[index] (the berserk player).</summary>
        public void GiveBerserkShotgun(int index)
        {
            if (!IsServer) return;
            var sor = StartOfRound.Instance;
            if (sor == null || sor.allPlayerScripts == null ||
                index < 0 || index >= sor.allPlayerScripts.Length) return;

            var item = FindShotgunItem();
            if (item == null || item.spawnPrefab == null)
            {
                Plugin.Log.LogWarning("[ChatChaos][Berserk] shotgun item not found in the item list.");
                return;
            }

            var player = sor.allPlayerScripts[index];
            var go = UnityEngine.Object.Instantiate(item.spawnPrefab,
                player.transform.position + Vector3.up * 0.6f, Quaternion.identity);

            var grab = go.GetComponent<GrabbableObject>();
            var netObj = go.GetComponent<NetworkObject>();
            if (grab == null || netObj == null)
            {
                Plugin.Log.LogWarning("[ChatChaos][Berserk] shotgun prefab missing GrabbableObject/NetworkObject.");
                UnityEngine.Object.Destroy(go);
                return;
            }

            try { grab.fallTime = 1f; } catch { }
            netObj.Spawn();
            _hostBerserkShotgun = grab;
            Plugin.Log.LogInfo($"[ChatChaos][Berserk] shotgun spawned for player index {index}.");

            Safe(() => GiveShotgunClientRpc(new NetworkObjectReference(netObj), index));
        }

        // Runs on EVERY machine (incl. host): only the target's owner actually grabs it.
        [ClientRpc]
        private void GiveShotgunClientRpc(NetworkObjectReference reference, int index)
        {
            if (!reference.TryGet(out var netObj)) return;
            var grab = netObj.GetComponent<GrabbableObject>();
            var sor = StartOfRound.Instance;
            if (grab == null || sor == null || sor.allPlayerScripts == null ||
                index < 0 || index >= sor.allPlayerScripts.Length) return;

            var player = sor.allPlayerScripts[index];
            if (player != sor.localPlayerController) return;   // only the owner grabs
            Core.BerserkShotgun.GrabAndHold(player, grab);
        }

        /// <summary>Host: remove the berserk shotgun (despawn it) and clear the ammo loop.</summary>
        public void RemoveBerserkShotgun()
        {
            if (!IsServer) return;
            if (_hostBerserkShotgun != null)
            {
                var no = _hostBerserkShotgun.GetComponent<NetworkObject>();
                if (no != null && no.IsSpawned) no.Despawn(true);
                _hostBerserkShotgun = null;
            }
            Core.BerserkShotgun.Clear();
            Safe(() => ClearShotgunHoldClientRpc());
        }

        [ClientRpc]
        private void ClearShotgunHoldClientRpc()
        {
            if (IsServer) return;
            Core.BerserkShotgun.Clear();
        }

        /// <summary>Detonates every landmine on the map. Each machine detonates locally
        /// (matching how the game processes a mine explosion per client).</summary>
        public void ExplodeAllMines()
        {
            if (!IsServer) return;
            DetonateAllMinesLocal();
            Safe(() => ExplodeAllMinesClientRpc());
        }

        [ClientRpc]
        private void ExplodeAllMinesClientRpc()
        {
            if (IsServer) return;
            DetonateAllMinesLocal();
        }

        /// <summary>Applies the winter-sale discounts (from a shared seed) on every machine.</summary>
        public void SetWinterSale(int seed)
        {
            if (!IsServer) return;
            Core.WinterSale.ApplyLocal(seed);
            Safe(() => SetWinterSaleClientRpc(seed));
        }

        [ClientRpc]
        private void SetWinterSaleClientRpc(int seed)
        {
            if (IsServer) return;
            Core.WinterSale.ApplyLocal(seed);
        }

        /// <summary>Restores the original store prices on every machine.</summary>
        public void EndWinterSale()
        {
            if (!IsServer) return;
            Core.WinterSale.RestoreLocal();
            Safe(() => EndWinterSaleClientRpc());
        }

        [ClientRpc]
        private void EndWinterSaleClientRpc()
        {
            if (IsServer) return;
            Core.WinterSale.RestoreLocal();
        }

        private static void DetonateAllMinesLocal()
        {
            int n = 0;
            foreach (var mine in UnityEngine.Object.FindObjectsOfType<Landmine>())
            {
                if (mine == null || mine.hasExploded) continue;
                try { mine.Detonate(); n++; } catch { }
            }
            Plugin.Log.LogInfo($"[ChatChaos][Mines] detonated {n} mine(s).");
        }

        private static Item? FindShotgunItem()
        {
            var sor = StartOfRound.Instance;
            if (sor == null || sor.allItemsList == null || sor.allItemsList.itemsList == null) return null;
            foreach (var it in sor.allItemsList.itemsList)
                if (it != null && it.spawnPrefab != null && it.spawnPrefab.GetComponent<ShotgunItem>() != null)
                    return it;
            return null;
        }

        private static void ToggleWeatherEffects(object tod, int activeIndex)
        {
            try
            {
                var effects = HarmonyLib.Traverse.Create(tod).Field("effects").GetValue() as System.Array;
                if (effects == null) return;

                for (int i = 0; i < effects.Length; i++)
                {
                    var eff = effects.GetValue(i);
                    if (eff == null) continue;

                    bool active = (i == activeIndex);   // activeIndex = -1 (None) -> all off
                    try { HarmonyLib.Traverse.Create(eff).Field("effectEnabled").SetValue(active); } catch { }

                    var obj = HarmonyLib.Traverse.Create(eff).Field("effectObject").GetValue() as UnityEngine.GameObject;
                    if (obj != null) obj.SetActive(active);

                    var pObj = HarmonyLib.Traverse.Create(eff).Field("effectPermanentObject").GetValue() as UnityEngine.GameObject;
                    if (pObj != null) pObj.SetActive(active);
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[ChatChaos][Weather] effect toggle failed: {e.Message}");
            }
        }

        private static void HealAllPlayersLocal()
        {
            var sor = StartOfRound.Instance;
            if (sor == null || sor.allPlayerScripts == null) return;

            var me = sor.localPlayerController;
            foreach (var p in sor.allPlayerScripts)
            {
                if (p == null || !p.isPlayerControlled || p.isPlayerDead) continue;

                p.health = MaxHp;   // keep every copy consistent across machines

                if (p == me)
                {
                    // Our own player: refresh the HUD and clear the injured/bleeding state.
                    p.criticallyInjured = false;
                    p.bleedingHeavily = false;
                    if (HUDManager.Instance != null)
                        HUDManager.Instance.UpdateHealthUI(MaxHp, false);
                }
            }
        }

        [ClientRpc]
        private void StartPollClientRpc(string l0, string l1, string l2, float duration)
        {
            if (IsServer) return; // host already rendered locally
            PollHud.Instance?.ShowPoll(l0, l1, l2, duration);
        }

        [ClientRpc]
        private void CountsClientRpc(int c0, int c1, int c2)
        {
            if (IsServer) return;
            PollHud.Instance?.UpdateCounts(c0, c1, c2);
        }

        [ClientRpc]
        private void EndPollClientRpc(int winnerIndex, int winnerCount)
        {
            if (IsServer) return;
            PollHud.Instance?.ShowResult(winnerIndex, winnerCount);
        }

        [ClientRpc]
        private void HideClientRpc()
        {
            if (IsServer) return;
            PollHud.Instance?.Hide();
        }

        [ClientRpc]
        private void TipClientRpc(string header, string body)
        {
            if (IsServer) return;
            GameTips.Show(header, body);
        }

        [ClientRpc]
        private void PauseClientRpc()
        {
            if (IsServer) return;
            PollHud.Instance?.Pause();
        }
    }
}
