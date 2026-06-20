using System;
using System.Reflection;
using ChatChaos.Core;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace ChatChaos.Networking
{
    /// <summary>
    /// Creates, registers and spawns the mod's network object (the one carrying
    /// ChatChaosNetworker). This is the required path to have custom RPCs in a
    /// Netcode for GameObjects game.
    ///
    ///   1) GameNetworkManager.Start -> build the prefab {NetworkObject + ChatChaosNetworker}
    ///      and register it as a known network prefab.
    ///   2) StartOfRound.Start       -> the HOST instantiates and spawns it (once), so
    ///      it is replicated to all clients, then starts the Twitch connection.
    ///
    /// The class-level [HarmonyPatch] is REQUIRED: Harmony's PatchAll() only scans
    /// classes that carry it.
    /// </summary>
    [HarmonyPatch]
    public static class NetworkObjectManager
    {
        private static GameObject? _networkPrefab;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameNetworkManager), "Start")]
        public static void RegisterPrefab()
        {
            if (_networkPrefab != null) return;

            try
            {
                if (NetworkManager.Singleton == null)
                {
                    Plugin.Log.LogError("NetworkObjectManager: NetworkManager.Singleton is null; cannot register prefab.");
                    return;
                }

                _networkPrefab = new GameObject("ChatChaosNetworkHandler");
                UnityEngine.Object.DontDestroyOnLoad(_networkPrefab);
                _networkPrefab.hideFlags = HideFlags.HideAndDontSave;

                var netObj = _networkPrefab.AddComponent<NetworkObject>();
                _networkPrefab.AddComponent<ChatChaosNetworker>();

                AssignStableHash(netObj, "ChatChaos.ChatChaosNetworker");

                NetworkManager.Singleton.AddNetworkPrefab(_networkPrefab);
                Plugin.Log.LogInfo("NetworkObjectManager: network prefab registered.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"NetworkObjectManager: failed to register prefab: {e}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartOfRound), "Start")]
        public static void SpawnHandler()
        {
            try
            {
                var nm = NetworkManager.Singleton;
                if (nm == null) return;
                if (!(nm.IsHost || nm.IsServer))
                {
                    Plugin.Log.LogInfo("NetworkObjectManager: client will receive the handler from the host.");
                    return;
                }
                if (ChatChaosNetworker.Instance != null)
                {
                    PollManager.OnHostReady();
                    return;
                }
                if (_networkPrefab == null)
                {
                    Plugin.Log.LogError("NetworkObjectManager: prefab is null (registration failed?); cannot spawn.");
                    return;
                }

                var instance = UnityEngine.Object.Instantiate(_networkPrefab);
                instance.GetComponent<NetworkObject>().Spawn(destroyWithScene: false);
                Plugin.Log.LogInfo("NetworkObjectManager: ChatChaosNetworker spawned by the host.");

                // Now that the host is up, open the Twitch connection.
                PollManager.OnHostReady();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"NetworkObjectManager: failed to spawn handler: {e}");
            }
        }

        private static void AssignStableHash(NetworkObject netObj, string key)
        {
            uint hash = (uint)key.GetHashCode();
            var field = typeof(NetworkObject).GetField(
                "GlobalObjectIdHash",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
            {
                Plugin.Log.LogError("NetworkObjectManager: GlobalObjectIdHash field not found; spawn will likely fail.");
                return;
            }
            field.SetValue(netObj, hash);
        }
    }
}
