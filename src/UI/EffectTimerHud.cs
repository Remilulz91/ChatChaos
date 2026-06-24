using System.Text;
using ChatChaos.Config;
using ChatChaos.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChatChaos.UI
{
    /// <summary>
    /// Small top-left list of the currently active time-limited effects with their
    /// countdown (e.g. "Time frozen  42s"). Reads EffectTimers every frame; hidden when
    /// nothing is active OR when we're not in a game (e.g. the main menu).
    ///
    /// Uses the game's own 3270 font (grabbed at runtime), with a dark shadow copy behind
    /// for readability over the world.
    /// </summary>
    public class EffectTimerHud : MonoBehaviour
    {
        public static EffectTimerHud? Instance { get; private set; }

        private static readonly Color TextColor   = new Color32(238, 238, 238, 255);
        private static readonly Color ShadowColor = new Color32(0, 0, 0, 217);

        private TextMeshProUGUI _text = null!;
        private TextMeshProUGUI _shadow = null!;
        private TMP_FontAsset? _gameFont;
        private bool _fontApplied;

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("ChatChaos_EffectTimers");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            Instance = go.AddComponent<EffectTimerHud>();
        }

        private void Awake() => Build();

        private void Update()
        {
            EnsureGameFont();

            // Hide entirely when not in a game (the HUD manager only exists in-game).
            // This stops stale timers from showing on the main menu.
            if (HUDManager.Instance == null)
            {
                if (_text.text.Length != 0) SetText("");
                return;
            }

            var active = EffectTimers.GetActive();
            if (active.Count == 0)
            {
                if (_text.text.Length != 0) SetText("");
                return;
            }

            var sb = new StringBuilder();
            foreach (var item in active)
                sb.Append(item.label).Append("  ").Append(item.secs).Append("s\n");
            SetText(sb.ToString().TrimEnd());
        }

        private void SetText(string s)
        {
            _text.text = s;
            _shadow.text = s;
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1050;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            // No GraphicRaycaster on purpose: display-only HUD, must not capture the cursor.

            // Configurable position; default = right edge, vertically centred.
            float ax = Mathf.Clamp01(ModConfig.TimerAnchorX.Value);
            float ay = Mathf.Clamp01(ModConfig.TimerAnchorY.Value);
            bool right = ax > 0.5f;
            var align = right ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;

            // Shadow first (renders behind), then the main text.
            _shadow = NewText("Shadow", align, ShadowColor, ax, ay, right);
            _shadow.rectTransform.anchoredPosition += new Vector2(2f, -2f);
            _text = NewText("Text", align, TextColor, ax, ay, right);
        }

        private TextMeshProUGUI NewText(string name, TextAlignmentOptions align, Color color,
                                        float ax, float ay, bool right)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(ax, 1f);                 // grow downward from the anchor
            rt.sizeDelta = new Vector2(560, 400);
            rt.anchoredPosition = new Vector2(right ? -20f : 20f, 0f);

            var t = go.GetComponent<TextMeshProUGUI>();
            if (_gameFont != null) t.font = _gameFont;
            t.fontSize = 30;
            t.fontStyle = FontStyles.Bold;
            t.alignment = align;
            t.color = color;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.text = "";
            return t;
        }

        private void EnsureGameFont()
        {
            if (_fontApplied) return;
            var f = GameFont.Find();
            if (f == null) return;
            _gameFont = f;
            _text.font = f;
            _shadow.font = f;
            _fontApplied = true;
        }
    }
}
