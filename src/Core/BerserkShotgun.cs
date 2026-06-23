using GameNetcodeStuff;
using ChatChaos.Logging;
using Unity.Netcode;
using UnityEngine;

namespace ChatChaos.Core
{
    /// <summary>
    /// Handles the Berserk shotgun on the player who holds it: forces the grab (freeing a
    /// slot first if the inventory is full) and keeps the shells topped up for unlimited
    /// ammo. The spawn/despawn itself is done by the networker (host).
    /// </summary>
    public static class BerserkShotgun
    {
        // The shotgun GrabbableObject the LOCAL player holds (set on the owner machine).
        private static object _held = null!;

        public static void Clear() => _held = null!;

        /// <summary>Force the local player to grab the spawned shotgun (owner machine only).</summary>
        public static void GrabAndHold(PlayerControllerB player, GrabbableObject grab)
        {
            try
            {
                // Free a slot: if the inventory is full, drop the currently held item.
                if (player.FirstEmptyItemSlot() == -1)
                    player.DiscardHeldObject();

                player.currentlyGrabbingObject = grab;
                player.GrabObjectServerRpc(new NetworkObjectReference(grab.NetworkObject));
                _held = grab;
                Log.Info("Berserk", "shotgun grabbed by the local player.");
            }
            catch (System.Exception e)
            {
                Log.Error("Berserk", $"shotgun grab failed: {e}");
            }
        }

        /// <summary>Per-machine: keep the held berserk shotgun loaded (unlimited ammo).</summary>
        public static void Tick()
        {
            if (_held == null) return;
            try
            {
                // Set shells via reflection so we don't depend on the ShotgunItem type/field.
                HarmonyLib.Traverse.Create(_held).Field("shellsLoaded").SetValue(2);
            }
            catch { /* not a shotgun / field renamed — ignore */ }
        }
    }
}
