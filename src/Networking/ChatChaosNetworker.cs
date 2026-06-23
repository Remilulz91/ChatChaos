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
