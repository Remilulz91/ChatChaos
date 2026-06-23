using ChatChaos.Config;

namespace ChatChaos.Logging
{
    /// <summary>
    /// Centralised, tagged logging so the BepInEx log is easy to scan when something goes
    /// wrong in game. Every line looks like:  [ChatChaos][Poll] ...  /  [ChatChaos][Event] ...
    ///
    /// Use a clear tag for each subsystem (Poll, Event, Twitch, Net, Weather, Ship, ...).
    /// Info/Warn/Error are always written. Debug() is only written when
    /// Config -> Debug -> VerboseLogging is enabled (so normal logs stay readable, and you
    /// can flip verbose on to capture every vote / every broadcast when hunting a bug).
    /// </summary>
    public static class Log
    {
        private const string Root = "ChatChaos";

        public static void Info(string tag, string message)
            => Plugin.Log.LogInfo($"[{Root}][{tag}] {message}");

        public static void Warn(string tag, string message)
            => Plugin.Log.LogWarning($"[{Root}][{tag}] {message}");

        public static void Error(string tag, string message)
            => Plugin.Log.LogError($"[{Root}][{tag}] {message}");

        /// <summary>Verbose detail — only written when VerboseLogging is on.</summary>
        public static void Debug(string tag, string message)
        {
            if (ModConfig.VerboseLogging != null && ModConfig.VerboseLogging.Value)
                Plugin.Log.LogInfo($"[{Root}][{tag}] {message}");
        }
    }
}
