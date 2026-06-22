using ChatChaos.Config;
using ChatChaos.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace ChatChaos.UI
{
    /// <summary>
    /// The on-screen poll panel — the orange "&gt;SONDAGE" box from the screenshots.
    ///
    /// It shows the title, a countdown, the voting instruction, and three rows whose
    /// bars grow with the live vote counts (the leader is highlighted). When voting
    /// ends it switches to the result view: the winning row turns green with a trophy
    /// and the others fade out.
    ///
    /// Built with Unity's uGUI in code (no asset bundle needed). Colours/positions are
    /// tuned to resemble the original; fine-tuning is easy via the constants below and
    /// the Display section of the config.
    /// </summary>
    public class PollHud : MonoBehaviour
    {
        public static PollHud? Instance { get; private set; }

        // ---- palette (tuned to match the screenshots) ----
        private static readonly Color PanelColor   = new Color32(214, 182, 122, 255);
        private static readonly Color HeaderText   = new Color32(38, 28, 16, 255);
        private static readonly Color RowEmpty      = new Color32(43, 43, 46, 255);
        private static readonly Color BarOrange     = new Color32(240, 168, 30, 255);
        private static readonly Color BarLeading    = new Color32(252, 196, 70, 255);
        private static readonly Color BarWinner     = new Color32(95, 190, 70, 255);
        private static readonly Color RowText        = new Color32(238, 238, 238, 255);

        private const int PanelWidth = 470;
        private const int PanelHeight = 232;
        private const int Pad = 16;
        private const int RowHeight = 34;
        private const int RowGap = 9;
        private const int MaxRows = 3;

        // ---- state ----
        private bool _active;
        private bool _finished;
        private bool _paused;
        private readonly string[] _labels = new string[MaxRows];
        private readonly int[] _counts = new int[MaxRows];
        private int _rowsUsed;
        private float _endTime;
        private float _autoHideTime = float.MaxValue;
        private int _winnerIndex = -1;
        private int _winnerCount;

        // ---- ui refs ----
        private Canvas _canvas = null!;
        private RectTransform _panel = null!;
        private Text _title = null!;
        private Text _timer = null!;
        private Text _instruction = null!;
        private Sprite _white = null!;

        private readonly Image[] _rowBg = new Image[MaxRows];
        private readonly Image[] _rowFill = new Image[MaxRows];
        private readonly Text[] _rowLabel = new Text[MaxRows];
        private readonly Text[] _rowCount = new Text[MaxRows];

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("ChatChaos_Hud");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            Instance = go.AddComponent<PollHud>();
        }

        private void Awake()
        {
            Build();
            SetVisible(false);
        }

        // ----------------------------------------------------------------- public API

        public void ShowPoll(string l0, string l1, string l2, float duration)
        {
            _labels[0] = l0 ?? ""; _labels[1] = l1 ?? ""; _labels[2] = l2 ?? "";
            _rowsUsed = 0;
            for (int i = 0; i < MaxRows; i++) { _counts[i] = 0; if (_labels[i].Length > 0) _rowsUsed = i + 1; }

            _finished = false;
            _paused = false;
            _winnerIndex = -1;
            _endTime = Time.time + duration;
            _autoHideTime = float.MaxValue;   // the voting panel never auto-hides
            _active = true;

            _title.text = Loc.Get("panel.title");
            _instruction.text = Loc.Get("panel.instruction");
            _instruction.color = HeaderText;

            SetVisible(true);
            Refresh();
        }

        public void UpdateCounts(int c0, int c1, int c2)
        {
            if (_paused) return;   // frozen: ignore late count updates
            _counts[0] = c0; _counts[1] = c1; _counts[2] = c2;
            if (_active && !_finished) Refresh();
        }

        /// <summary>
        /// Freezes the current voting panel (countdown and vote counts stop moving) and
        /// keeps it on screen, then clears it after ResultDisplayDuration. Used when the
        /// ship leaves the moon mid-poll: the poll is cancelled, no winner is applied.
        /// </summary>
        public void Pause()
        {
            if (!_active || _finished) return;   // nothing to pause (or already a result)
            _paused = true;
            _autoHideTime = Time.time + Mathf.Max(1f, ModConfig.ResultDisplayDuration.Value);
            SetVisible(true);
        }

        public void ShowResult(int winnerIndex, int winnerCount)
        {
            _finished = true;
            _paused = false;
            _winnerIndex = winnerIndex;
            _winnerCount = winnerCount;
            _instruction.text = Loc.Get("panel.finished");

            // Self-contained timer: the result panel clears on its own after the
            // configured duration, even if the host's "hide" network message is lost.
            _autoHideTime = Time.time + Mathf.Max(1f, ModConfig.ResultDisplayDuration.Value);
            _active = true;

            SetVisible(true);
            Refresh();
        }

        public void Hide()
        {
            _active = false;
            _finished = false;
            _paused = false;
            _autoHideTime = float.MaxValue;
            SetVisible(false);
        }

        // ----------------------------------------------------------------- per-frame

        private void Update()
        {
            if (!_active) return;

            if (_paused)
            {
                // Frozen panel (ship left mid-poll): leave the timer and counts as they
                // were, then clear the panel after the delay. Poll is cancelled.
                if (Time.time >= _autoHideTime) Hide();
                return;
            }

            if (!_finished)
            {
                int secs = Mathf.Max(0, Mathf.CeilToInt(_endTime - Time.time));
                _timer.text = secs + "s";
            }
            else
            {
                _timer.text = "";
                // Result view clears itself after ResultDisplayDuration (set in ShowResult),
                // so the panel always disappears completely on its own.
                if (Time.time >= _autoHideTime) Hide();
            }
        }

        // ----------------------------------------------------------------- rendering

        private void Refresh()
        {
            int max = 1;
            for (int i = 0; i < _rowsUsed; i++) max = Mathf.Max(max, _counts[i]);

            // Which row currently leads (for the brighter highlight while voting).
            int leader = -1, leadVal = -1;
            for (int i = 0; i < _rowsUsed; i++)
                if (_counts[i] > leadVal) { leadVal = _counts[i]; leader = i; }
            if (leadVal <= 0) leader = -1;

            for (int i = 0; i < MaxRows; i++)
            {
                bool used = i < _rowsUsed;
                _rowBg[i].gameObject.SetActive(used);
                if (!used) continue;

                bool isWinner = _finished && i == _winnerIndex;
                bool dimmed = _finished && i != _winnerIndex;

                // Bar width: full for the winner, proportional otherwise.
                float frac = isWinner ? 1f : Mathf.Clamp01((float)_counts[i] / max);
                float innerWidth = PanelWidth - Pad * 2;
                var fillRt = (RectTransform)_rowFill[i].transform;
                fillRt.sizeDelta = new Vector2(innerWidth * frac, RowHeight);

                Color fillColor = isWinner ? BarWinner : (i == leader ? BarLeading : BarOrange);
                _rowFill[i].color = fillColor;
                _rowFill[i].gameObject.SetActive(isWinner || _counts[i] > 0);

                _rowLabel[i].text = $"{i + 1}|{_labels[i]}";
                _rowLabel[i].color = dimmed ? new Color(1f, 1f, 1f, 0.45f) : RowText;

                string trophy = isWinner ? "  \U0001F3C6" : "";
                _rowCount[i].text = _counts[i] + trophy;
                _rowCount[i].color = dimmed ? new Color(1f, 1f, 1f, 0.45f) : RowText;
            }
        }

        // ----------------------------------------------------------------- ui build

        private void Build()
        {
            _white = MakeWhiteSprite();

            // Canvas (screen-space overlay, drawn on top of the game HUD).
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 1000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // Panel.
            float ax = Mathf.Clamp01(ModConfig.HudAnchorX.Value);
            float ay = Mathf.Clamp01(ModConfig.HudAnchorY.Value);
            float scale = Mathf.Max(0.3f, ModConfig.HudScale.Value);

            var panelImg = NewImage(transform, "Panel", PanelColor);
            _panel = (RectTransform)panelImg.transform;
            _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(ax, ay);
            _panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            _panel.anchoredPosition = Vector2.zero;
            _panel.localScale = new Vector3(scale, scale, 1f);

            // Title (top-left).
            _title = NewText(_panel, "Title", ">SONDAGE", 30, TextAnchor.UpperLeft, HeaderText, FontStyle.Bold);
            Place(_title.rectTransform, Pad, Pad, PanelWidth - Pad * 2, 36);

            // Timer (top-right).
            _timer = NewText(_panel, "Timer", "", 26, TextAnchor.UpperRight, HeaderText, FontStyle.Bold);
            Place(_timer.rectTransform, Pad, Pad, PanelWidth - Pad * 2, 34);

            // Instruction line.
            _instruction = NewText(_panel, "Instruction", "", 18, TextAnchor.UpperLeft, HeaderText, FontStyle.Normal);
            Place(_instruction.rectTransform, Pad, Pad + 42, PanelWidth - Pad * 2, 26);

            // Rows.
            int top = Pad + 76;
            for (int i = 0; i < MaxRows; i++)
            {
                int y = top + i * (RowHeight + RowGap);

                var bg = NewImage(_panel, "RowBg" + i, RowEmpty);
                _rowBg[i] = bg;
                Place((RectTransform)bg.transform, Pad, y, PanelWidth - Pad * 2, RowHeight);

                var fill = NewImage(bg.transform, "RowFill" + i, BarOrange);
                _rowFill[i] = fill;
                var frt = (RectTransform)fill.transform;
                frt.anchorMin = new Vector2(0, 1); frt.anchorMax = new Vector2(0, 1); frt.pivot = new Vector2(0, 1);
                frt.anchoredPosition = Vector2.zero;
                frt.sizeDelta = new Vector2(0, RowHeight);

                var label = NewText(bg.transform, "RowLabel" + i, "", 18, TextAnchor.MiddleLeft, RowText, FontStyle.Bold);
                Place(label.rectTransform, 10, 0, PanelWidth - Pad * 2 - 70, RowHeight);
                label.rectTransform.anchoredPosition = new Vector2(10, 0);
                StretchVert(label.rectTransform);

                var count = NewText(bg.transform, "RowCount" + i, "", 18, TextAnchor.MiddleRight, RowText, FontStyle.Bold);
                var crt = count.rectTransform;
                crt.anchorMin = new Vector2(1, 0); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(1, 0.5f);
                crt.sizeDelta = new Vector2(90, 0);
                crt.anchoredPosition = new Vector2(-10, 0);
                _rowLabel[i] = label;
                _rowCount[i] = count;
            }
        }

        private void SetVisible(bool v)
        {
            if (_panel != null) _panel.gameObject.SetActive(v);
        }

        // ---- small uGUI builders ----

        private Image NewImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = _white;
            img.type = Image.Type.Simple;
            img.color = color;
            return img;
        }

        private Text NewText(Transform parent, string name, string text, int size,
                             TextAnchor anchor, Color color, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.text = text;
            t.font = GetFont();
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>Anchors a rect to the panel's TOP-LEFT and positions it (x right, y down).</summary>
        private static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
        }

        private static void StretchVert(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
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
