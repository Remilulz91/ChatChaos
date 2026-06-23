using UnityEngine;
using UnityEngine.UI;

namespace ChatChaos.UI
{
    /// <summary>
    /// Full-screen "RECEIVING SIGNAL" -> "GO BERSERK" overlay played when the Berserk
    /// event wins, typed letter by letter. Purely visual; it self-hides after the
    /// animation. The actual berserk effect is started by the host once this finishes.
    /// </summary>
    public class BerserkHud : MonoBehaviour
    {
        public static BerserkHud? Instance { get; private set; }

        private static readonly Color SignalColor = new Color32(80, 240, 90, 255);
        private static readonly Color BerserkColor = new Color32(230, 40, 40, 255);

        private const string SignalLine = "RECEIVING SIGNAL";
        private const string BerserkLine = "GO BERSERK";
        private const float SignalTypeDur = 1.3f;   // time to type the signal line
        private const float BerserkStart = 1.7f;    // when "GO BERSERK" starts typing
        private const float BerserkTypeDur = 1.1f;
        private const float TotalDur = 3.4f;         // total overlay time

        private RectTransform _panel = null!;
        private Text _signal = null!;
        private Text _berserk = null!;
        private Sprite _white = null!;
        private bool _active;
        private float _start;

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("ChatChaos_BerserkHud");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            Instance = go.AddComponent<BerserkHud>();
        }

        private void Awake()
        {
            Build();
            SetVisible(false);
        }

        public void ShowSignal()
        {
            _active = true;
            _start = Time.time;
            _signal.text = "";
            _berserk.text = "";
            SetVisible(true);
        }

        private void Update()
        {
            if (!_active) return;
            float t = Time.time - _start;

            int sc = Mathf.Clamp(Mathf.FloorToInt(SignalLine.Length * (t / SignalTypeDur)), 0, SignalLine.Length);
            _signal.text = SignalLine.Substring(0, sc);

            if (t >= BerserkStart)
            {
                float bp = (t - BerserkStart) / BerserkTypeDur;
                int bc = Mathf.Clamp(Mathf.FloorToInt(BerserkLine.Length * bp), 0, BerserkLine.Length);
                _berserk.text = BerserkLine.Substring(0, bc);
                // subtle flicker once it's showing
                float a = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(t * 12f));
                _berserk.color = new Color(BerserkColor.r, BerserkColor.g, BerserkColor.b, a);
            }

            if (t >= TotalDur) { _active = false; SetVisible(false); }
        }

        private void Build()
        {
            _white = MakeWhiteSprite();

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1100;   // above the poll panel
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            // No GraphicRaycaster on purpose: display-only HUD, must not capture the cursor.

            var panelGo = new GameObject("Panel", typeof(RectTransform));
            panelGo.transform.SetParent(transform, false);
            _panel = (RectTransform)panelGo.transform;
            Stretch(_panel);

            var bg = NewImage(_panel, "BG", new Color(0f, 0f, 0f, 0.45f));
            Stretch((RectTransform)bg.transform);

            _signal = NewText(_panel, "Signal", 54, SignalColor, FontStyle.Bold);
            var srt = _signal.rectTransform;
            srt.anchorMin = new Vector2(0, 0.60f); srt.anchorMax = new Vector2(1, 0.72f);
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;

            _berserk = NewText(_panel, "Berserk", 96, BerserkColor, FontStyle.Bold);
            var brt = _berserk.rectTransform;
            brt.anchorMin = new Vector2(0, 0.38f); brt.anchorMax = new Vector2(1, 0.58f);
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
        }

        private void SetVisible(bool v) { if (_panel != null) _panel.gameObject.SetActive(v); }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private Image NewImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = _white;
            img.color = color;
            return img;
        }

        private Text NewText(Transform parent, string name, int size, Color color, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = GetFont();
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static Font GetFont()
        {
            Font? f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
            if (f == null) f = Font.CreateDynamicFontFromOSFont("Consolas", 16);
            return f!;
        }

        private static Sprite MakeWhiteSprite()
        {
            var tex = Texture2D.whiteTexture;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
