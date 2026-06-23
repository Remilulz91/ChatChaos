using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ChatChaos.Config;
using ChatChaos.Core;
using ChatChaos.Localization;

namespace ChatChaos
{
    /// <summary>
    /// Mod entry point, loaded by BepInEx when the game starts.
    ///
    /// Responsibilities:
    ///   1. Register with BepInEx (the [BepInPlugin] attribute).
    ///   2. Load the configuration (Twitch token, durations, prefix, language...).
    ///   3. Initialise localization (FR / EN, auto-detected from the game language).
    ///   4. Register the random events that the chat can vote for.
    ///   5. Apply every Harmony patch (the code that hooks into the game).
    ///   6. Start the runtime ticker that drives the poll lifecycle.
    ///
    /// Kept intentionally small: all real logic lives in the other files. This
    /// class just wires the pieces together.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        // --- Mod identity (keep in sync with manifest.json and the .csproj) ---
        public const string Author = "Remilulz_91";
        public const string Guid = "Remilulz_91.ChatChaos";
        public const string Name = "ChatChaos";
        public const string Version = "0.25.0";
        // Inspired by Sehelitar's Twitch-integration mod (moderator for MrTiboute).

        /// <summary>Singleton instance, accessible anywhere via Plugin.Instance.</summary>
        public static Plugin Instance { get; private set; } = null!;

        /// <summary>Shared logger: Plugin.Log.LogInfo("...").</summary>
        public static ManualLogSource Log { get; private set; } = null!;

        private readonly Harmony _harmony = new Harmony(Guid);

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            // 1) Configuration (Twitch credentials, poll timing, prefix, language...).
            ModConfig.Init(base.Config);

            // 2) Localization (decides FR vs EN once, based on config + game language).
            Loc.Init();

            // 3) Register the random events the chat can vote for.
            //    You add your own events in EventRegistry.RegisterDefaults().
            EventRegistry.RegisterDefaults();

            // 4) Apply ALL Harmony patches found in this assembly.
            try
            {
                _harmony.PatchAll();

                // 5) Start the ticker (poll scheduling, vote counting, expiry).
                PollTicker.EnsureExists();

                Log.LogInfo($"{Name} v{Version} by {Author} loaded. " +
                            $"{EventRegistry.Count} event(s) registered. Language: {Loc.Current}.");
                Log.LogInfo("Inspired by Sehelitar's Twitch-integration mod (moderator for MrTiboute).");
            }
            catch (Exception e)
            {
                Log.LogError($"Failed to apply Harmony patches: {e}");
            }
        }
    }
}
