namespace ChatChaos.UI
{
    /// <summary>
    /// Shows the game's native on-screen "tip" box (the bottom-left sticky note used
    /// for hints). We use it to confirm on screen that the mod is live and synced
    /// when the ship lands.
    /// </summary>
    public static class GameTips
    {
        public static void Show(string header, string body)
        {
            try
            {
                HUDManager.Instance?.DisplayTip(header, body);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"ChatChaos: could not show on-screen tip: {e.Message}");
            }
        }
    }
}
