using TMPro;
using UnityEngine;

namespace ChatChaos.UI
{
    /// <summary>
    /// Finds the game's own TMP font (3270font, the IBM-3270 pixel font Lethal Company
    /// uses for its whole UI) at runtime, so every ChatChaos HUD renders in the same font
    /// as the vanilla game. Read from the live HUD by reflection (no compile-time field
    /// dependency); falls back to the default TMP font if the HUD isn't up yet.
    /// </summary>
    public static class GameFont
    {
        public static TMP_FontAsset? Find()
        {
            try
            {
                var hud = HUDManager.Instance;
                if (hud != null)
                {
                    var tips = HarmonyLib.Traverse.Create(hud).Field("controlTipLines").GetValue() as TextMeshProUGUI[];
                    if (tips != null)
                        foreach (var t in tips)
                            if (t != null && t.font != null) return t.font;
                }
            }
            catch { }
            try { return TMP_Settings.defaultFontAsset; } catch { return null; }
        }
    }
}
