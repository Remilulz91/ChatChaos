using BepInEx.Configuration;

namespace ChatChaos.Config
{
    /// <summary>
    /// Mod configuration, exposed in the file
    /// BepInEx/config/Remilulz_91.ChatChaos.cfg (created on first launch).
    ///
    /// Everything tunable lives here so there are no "magic numbers" scattered
    /// across the code.
    ///
    /// SECURITY: the Twitch OAuth token is a SECRET. It stays in this local .cfg
    /// only and must never be committed to a public repo (it is git-ignored).
    /// It only grants chat read/write for the account, and can be revoked anytime.
    /// </summary>
    public static class ModConfig
    {
        // ---------------- Twitch ----------------

        /// <summary>Master switch for the Twitch integration.</summary>
        public static ConfigEntry<bool> TwitchEnabled = null!;

        /// <summary>
        /// OAuth token for the account that reads votes and posts announcements.
        /// Accepts the raw token or the "oauth:" prefixed form. Generate one with
        /// the chat scopes chat:read + chat:edit (see the README).
        /// </summary>
        public static ConfigEntry<string> TwitchOAuthToken = null!;

        /// <summary>Channel to join and post in (your Twitch login, lowercase).</summary>
        public static ConfigEntry<string> TwitchChannel = null!;

        /// <summary>
        /// Account login that the token belongs to. Leave empty to reuse the channel
        /// name (the usual case: you read and post on your own channel).
        /// </summary>
        public static ConfigEntry<string> TwitchUsername = null!;

        /// <summary>Use TLS (port 6697) instead of plain IRC (port 6667).</summary>
        public static ConfigEntry<bool> TwitchUseSsl = null!;

        // ---------------- Poll behaviour ----------------

        /// <summary>Seconds after landing on a moon before the first poll starts.</summary>
        public static ConfigEntry<float> PollDelayAfterLanding = null!;

        /// <summary>How many seconds a poll stays open for voting.</summary>
        public static ConfigEntry<float> PollDuration = null!;

        /// <summary>
        /// Seconds between polls while on the same moon. 0 = only ONE poll per
        /// landing (the default, matching the original design). Set e.g. 150 to keep
        /// the chaos going with a fresh poll every 150s until the ship leaves.
        /// </summary>
        public static ConfigEntry<float> PollRepeatInterval = null!;

        /// <summary>Seconds the result panel (winner) stays on screen after a poll.</summary>
        public static ConfigEntry<float> ResultDisplayDuration = null!;

        /// <summary>Skip polls on the free "safe" moon (the Company building).</summary>
        public static ConfigEntry<bool> SkipCompanyMoon = null!;

        // ---------------- Chat messages ----------------

        /// <summary>Post poll announcements (start, options, winner) in chat.</summary>
        public static ConfigEntry<bool> AnnounceInChat = null!;

        /// <summary>Prefix put in front of every chat message the mod posts.</summary>
        public static ConfigEntry<string> ChatPrefix = null!;

        /// <summary>Also show poll messages in the in-game chat box (seen by all players).</summary>
        public static ConfigEntry<bool> ShowInGameChat = null!;

        /// <summary>HTML hex colour of the prefix in the in-game chat (e.g. F0A91E).</summary>
        public static ConfigEntry<string> InGameChatColorHex = null!;

        // ---------------- Display ----------------

        /// <summary>Language: Auto (from the game), English or French.</summary>
        public static ConfigEntry<string> Language = null!;

        /// <summary>Horizontal position of the poll panel (0 = left, 1 = right).</summary>
        public static ConfigEntry<float> HudAnchorX = null!;

        /// <summary>Vertical position of the poll panel (0 = bottom, 1 = top).</summary>
        public static ConfigEntry<float> HudAnchorY = null!;

        /// <summary>Overall scale of the poll panel.</summary>
        public static ConfigEntry<float> HudScale = null!;

        public static void Init(ConfigFile cfg)
        {
            // -- Twitch --
            TwitchEnabled = cfg.Bind(
                "Twitch", "Enabled", true,
                "Turn the Twitch integration on/off. When off, polls still run but votes " +
                "come from nobody (so the mod picks a random option).");

            TwitchOAuthToken = cfg.Bind(
                "Twitch", "OAuthToken", "",
                "SECRET. OAuth token for your account (scopes chat:read + chat:edit). " +
                "Raw or 'oauth:'-prefixed both work. NEVER share this token. See README to generate it.");

            TwitchChannel = cfg.Bind(
                "Twitch", "Channel", "",
                "The Twitch channel to read votes from and post in (your login, lowercase).");

            TwitchUsername = cfg.Bind(
                "Twitch", "Username", "",
                "The login the token belongs to. Leave empty to reuse the Channel value.");

            TwitchUseSsl = cfg.Bind(
                "Twitch", "UseSSL", false,
                "Connect over TLS (port 6697). Default false (plain IRC, port 6667) which is " +
                "the most compatible. Try true if your network blocks the plain port.");

            // -- Poll --
            PollDelayAfterLanding = cfg.Bind(
                "Poll", "DelayAfterLanding", 45f,
                "Seconds after the ship lands on a moon before the first poll opens.");

            PollDuration = cfg.Bind(
                "Poll", "Duration", 60f,
                "How long a poll stays open for voting, in seconds.");

            PollRepeatInterval = cfg.Bind(
                "Poll", "RepeatInterval", 0f,
                "Seconds between polls on the same moon. 0 = a single poll per landing. " +
                "Set e.g. 150 to run a new poll repeatedly until the ship leaves.");

            ResultDisplayDuration = cfg.Bind(
                "Poll", "ResultDisplayDuration", 10f,
                "How long the winner panel stays on screen after voting ends, in seconds.");

            SkipCompanyMoon = cfg.Bind(
                "Poll", "SkipCompanyMoon", true,
                "Do not run polls on the free 'safe' moon (the Company building).");

            // -- Chat --
            AnnounceInChat = cfg.Bind(
                "Chat", "AnnounceInChat", true,
                "Post poll messages (start, options, winner) in the Twitch chat under your account.");

            ChatPrefix = cfg.Bind(
                "Chat", "Prefix", "[ChatChaos]",
                "Prefix added to every chat message the mod posts.");

            ShowInGameChat = cfg.Bind(
                "Chat", "ShowInGameChat", true,
                "Also display poll messages (landing, start + options, winner, takeoff) in the " +
                "in-game text chat, visible to every player in the lobby.");

            InGameChatColorHex = cfg.Bind(
                "Chat", "InGameChatColor", "F0A91E",
                "HTML hex colour (no '#') of the mod prefix in the in-game chat. Default F0A91E (orange).");

            // -- Display --
            Language = cfg.Bind(
                "Display", "Language", "Auto",
                "Language of the on-screen panel and chat messages: Auto, English or French. " +
                "Auto = French if the game/system language is French, English otherwise.");

            HudAnchorX = cfg.Bind(
                "Display", "PanelAnchorX", 0.30f,
                "Horizontal position of the poll panel (0 = far left, 1 = far right).");

            HudAnchorY = cfg.Bind(
                "Display", "PanelAnchorY", 0.22f,
                "Vertical position of the poll panel (0 = bottom, 1 = top).");

            HudScale = cfg.Bind(
                "Display", "PanelScale", 1.0f,
                "Overall scale of the poll panel (1 = default size).");
        }

        /// <summary>Effective account login (Username, or Channel if empty).</summary>
        public static string EffectiveUsername()
        {
            var u = (TwitchUsername.Value ?? "").Trim();
            return u.Length > 0 ? u : (TwitchChannel.Value ?? "").Trim();
        }
    }
}
