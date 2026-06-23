using ChatChaos.Config;
using ChatChaos.Twitch;
using ChatChaos.UI;

namespace ChatChaos.Core
{
    /// <summary>
    /// Sends a message to the Twitch chat (plain, under the host's account) AND, when
    /// enabled, to the in-game chat box (rich text with a coloured prefix). Shared by the
    /// poll system and by events that need to announce something.
    /// </summary>
    public static class ChatAnnounce
    {
        public static void Say(string text, bool inGame = true)
        {
            string prefix = (ModConfig.ChatPrefix.Value ?? "").Trim();

            // Twitch chat (plain text).
            if (ModConfig.AnnounceInChat.Value)
                TwitchClient.Instance?.SendMessage(prefix.Length > 0 ? prefix + " " + text : text);

            // In-game chat box (rich text, broadcast to every player by the host).
            if (inGame && ModConfig.ShowInGameChat.Value)
            {
                string color = (ModConfig.InGameChatColorHex.Value ?? "").Trim();
                string head = prefix.Length == 0
                    ? ""
                    : (color.Length > 0 ? $"<color=#{color}>{prefix}</color> " : prefix + " ");
                GameChat.Show(head + text);
            }
        }
    }
}
