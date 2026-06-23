using GameNetcodeStuff;
using HarmonyLib;

namespace ChatChaos.Patches
{
    /// <summary>
    /// Makes the active Berserk player immune to ALL damage by skipping DamagePlayer and
    /// KillPlayer for that player (covers mobs, turrets, mines, fall damage, etc.).
    /// </summary>
    [HarmonyPatch]
    public static class BerserkPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerControllerB), "DamagePlayer")]
        public static bool DamagePrefix(PlayerControllerB __instance)
            => !Core.Berserk.IsInvincible(__instance);   // false skips the original (no damage)

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerControllerB), "KillPlayer")]
        public static bool KillPrefix(PlayerControllerB __instance)
            => !Core.Berserk.IsInvincible(__instance);
    }
}
