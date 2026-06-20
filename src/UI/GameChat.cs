namespace ChatChaos.UI
{
    /// <summary>
    /// Posts a message into the GAME's text chat box (the one players type in,
    /// top-left), visible to everyone in the lobby.
    ///
    /// We call HUDManager.AddTextToChatOnServer on the HOST: the game then relays the
    /// message to every player through its own (already-patched) chat RPC, so we don't
    /// need a custom RPC for this. Must be called on the host/server.
    ///
    /// The message may contain TMP rich-text tags (e.g. &lt;color=#F0A91E&gt;...&lt;/color&gt;),
    /// which the in-game chat renders.
    /// </summary>
    public static class GameChat
    {
        public static void Show(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            try
            {
                var hud = HUDManager.Instance;
                if (hud == null) return;
                hud.AddTextToChatOnServer(message);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"ChatChaos: could not post in-game chat: {e.Message}");
            }
        }
    }
}
