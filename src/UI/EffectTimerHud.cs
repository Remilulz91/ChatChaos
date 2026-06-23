using System.Text;
using ChatChaos.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ChatChaos.UI
{
    /// <summary>
    /// Small top-left list of the currently active time-limited effects with their
    /// countdown (e.g. "Time frozen  42s"). Reads EffectTimers every frame; hidden when
    /// nothing is active.
    /// </summary>
    public class EffectTimerHud : MonoBehaviour
    {
        public static EffectTimerHud? Instance { get; private set; }

        private static readonly Color TextColor = new Color32(238, 238, 238, 255);

        private Text _text = null!;

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
            var active = EffectTimers.GetActive();
            if (active.Count == 0)
            {
                if (_text.text.Length != 0) _text.text = "";
                return;
            }

            var sb = new StringBuilder();
            foreach (var item in active)
                sb.Append(item.label).Append("  ").Append(item.secs).Append("s\n");
            _text.text = sb.ToString().TrimEnd();
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
            gameObject.AddComponent<GraphicRaycaster>();

            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(520, 400);
            rt.anchoredPosition = new Vector2(24, -120);   // top-left, below the game's top HUD

            _text = go.GetComponent<Text>();
            _text.font = GetFont();
            _text.fontSize = 24;
            _text.fontStyle = FontStyle.Bold;
            _text.alignment = TextAnchor.UpperLeft;
            _text.color = TextColor;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.text = "";

            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        private static Font GetFont()
        {
            Font? f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
            if (f == null) f = Font.CreateDynamicFontFromOSFont("Consolas", 16);
            return f!;
        }
    }
}
