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
            var terminal = Object.FindObjectOfType<Terminal>();
            if (terminal == null) return;
            terminal.groupCredits = credits;
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
