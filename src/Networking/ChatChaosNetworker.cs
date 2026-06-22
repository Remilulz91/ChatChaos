using System;
using ChatChaos.UI;
using Unity.Netcode;

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
