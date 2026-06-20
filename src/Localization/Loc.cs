using System.Collections.Generic;
using ChatChaos.Config;
using UnityEngine;

namespace ChatChaos.Localization
{
    public enum Lang { English, French }

    /// <summary>
    /// Tiny localization helper. Picks French when the game/system language is
    /// French (or when forced in the config), English otherwise.
    ///
    /// To add a language later: add it to the Lang enum and fill a dictionary in
    /// Init(). To add a new string: add a key to every dictionary and call
    /// Loc.Get("key") / Loc.Format("key", args).
    /// </summary>
    public static class Loc
    {
        public static Lang Current { get; private set; } = Lang.English;

        private static readonly Dictionary<Lang, Dictionary<string, string>> _strings = new();

        public static void Init()
        {
            Build();

            string pref = (ModConfig.Language.Value ?? "Auto").Trim().ToLowerInvariant();
            if (pref == "french" || pref == "fr" || pref == "français" || pref == "francais")
                Current = Lang.French;
            else if (pref == "english" || pref == "en")
                Current = Lang.English;
            else
                Current = DetectGameLanguage();
        }

        private static Lang DetectGameLanguage()
        {
            // The base game has no public language API we can rely on across versions,
            // so we read the OS/Unity language. French OS -> French panel & messages.
            return Application.systemLanguage == SystemLanguage.French ? Lang.French : Lang.English;
        }

        /// <summary>Returns the localized string for a key (falls back to English, then the key).</summary>
        public static string Get(string key)
        {
            if (_strings.TryGetValue(Current, out var map) && map.TryGetValue(key, out var v))
                return v;
            if (_strings.TryGetValue(Lang.English, out var en) && en.TryGetValue(key, out var ev))
                return ev;
            return key;
        }

        /// <summary>Returns the localized string with {0}, {1}... substituted.</summary>
        public static string Format(string key, params object[] args) => string.Format(Get(key), args);

        private static void Build()
        {
            _strings[Lang.English] = new Dictionary<string, string>
            {
                ["panel.title"]        = ">POLL",
                ["panel.instruction"]  = "Type 1, 2 or 3 in chat to vote.",
                ["panel.finished"]     = "Voting is over.",
                ["chat.start"]         = "A new vote starts! Type its number in the chat to make your choice.",
                ["chat.options"]       = "1 - {0}, 2 - {1}, 3 - {2}",
                ["chat.ending"]        = "Last call! Voting closes in {0}s.",
                ["chat.winner"]        = "Voting closed! Winner: {0} with {1} votes.",
                ["chat.winner.novote"] = "Nobody voted, so fate decides: {0}!",
                ["chat.landed"]        = "The ship has landed on {0}. Voting starts in {1} seconds.",
                ["chat.takeoff"]       = "The ship has taken off. Polls are interrupted.",
                ["tip.header"]         = "ChatChaos - Twitch active",
                ["tip.landed"]         = "Landed on {0}. Automatic polls active: first vote in {1}s.",
            };

            _strings[Lang.French] = new Dictionary<string, string>
            {
                ["panel.title"]        = ">SONDAGE",
                ["panel.instruction"]  = "Envoyez 1, 2 ou 3 dans le tchat pour voter.",
                ["panel.finished"]     = "Les votes sont terminés.",
                ["chat.start"]         = "Un nouveau vote démarre ! Faites votre choix en tapant son numéro dans le chat.",
                ["chat.options"]       = "1 - {0}, 2 - {1}, 3 - {2}",
                ["chat.ending"]        = "Derniers instants ! Le vote se termine dans {0}s.",
                ["chat.winner"]        = "Vote terminé ! Gagnant : {0} avec {1} votes.",
                ["chat.winner.novote"] = "Personne n'a voté, le sort décide : {0} !",
                ["chat.landed"]        = "Le vaisseau vient d'atterrir sur {0}. Les votes démarreront dans {1} secondes.",
                ["chat.takeoff"]       = "Le vaisseau vient de décoller. Les sondages sont interrompus.",
                ["tip.header"]         = "ChatChaos - Twitch activé",
                ["tip.landed"]         = "Atterrissage sur {0}. Sondages automatiques actifs : premier vote dans {1}s.",
            };
        }
    }
}
