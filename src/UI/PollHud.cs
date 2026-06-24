using ChatChaos.Config;
using ChatChaos.Localization;
using TMPro;
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
        private static readonly Color BarWinner     = new Color32(95, 190, 70, 255);
        private static readonly Color RowText        = new Color32(238, 238, 238, 255);
        private static readonly Color AlarmRed       = new Color32(214, 48, 40, 255);
        private static readonly Color Gold           = new Color32(242, 196, 72, 255);   // winner trophy

        private const int PanelWidth = 560;
        private const int PanelHeight = 272;
        private const int ResultHeight = 166;   // compact panel when only the winner is shown
        private const int Pad = 18;
        private const int RowHeight = 42;
        private const int RowGap = 11;
        private const int MaxRows = 3;

        // Vertical layout (derived once so the panel and rows stay in sync).
        private const int TitleH = 42;
        private const int InstrY = Pad + TitleH + 6;        // instruction line top
        private const int InstrH = 30;
        private const int RowsTop = InstrY + InstrH + 10;   // first row top

        // ---- state ----
        private bool _active;
        private bool _finished;
        private bool _paused;
        private bool _pausedPulse;
        private readonly string[] _labels = new string[MaxRows];
        private readonly int[] _counts = new int[MaxRows];
        private int _rowsUsed;
        private float _endTime;
        private float _autoHideTime = float.MaxValue;
        private float _votingHardDeadline = float.MaxValue;
        private int _winnerIndex = -1;
        private int _winnerCount;

        // ---- ui refs ----
        private Canvas _canvas = null!;
        private RectTransform _panel = null!;
        private TextMeshProUGUI _title = null!;
        private TextMeshProUGUI _timer = null!;
        private Image _clockIcon = null!;
        private Image _trophyIcon = null!;
        private TextMeshProUGUI _instruction = null!;
        private Sprite _white = null!;
        private TMP_FontAsset? _gameFont;
        private bool _fontApplied;

        private readonly Image[] _rowBg = new Image[MaxRows];
        private readonly Image[] _rowFill = new Image[MaxRows];
        private readonly TextMeshProUGUI[] _rowLabel = new TextMeshProUGUI[MaxRows];
        private readonly TextMeshProUGUI[] _rowCount = new TextMeshProUGUI[MaxRows];

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
            _pausedPulse = false;
            _winnerIndex = -1;
            _endTime = Time.time + duration;
            _autoHideTime = float.MaxValue;   // the voting panel never auto-hides...
            _votingHardDeadline = Time.time + duration + 12f;   // ...except this safety net
            _active = true;

            EnsureGameFont();
            _title.text = Loc.Get("panel.title");
            _instruction.text = Loc.Get("panel.instruction");
            _instruction.color = HeaderText;

            if (_clockIcon != null) _clockIcon.gameObject.SetActive(true);
            ApplyTimerVisual(1f, HeaderText);

            SetPanelHeight(PanelHeight);   // full size for the 3-option list
            SetVisible(true);
            Refresh();
        }

        /// <summary>Resizes the panel, keeping its TOP edge fixed (so content doesn't jump).</summary>
        private void SetPanelHeight(float height)
        {
            float ay = Mathf.Clamp01(ModConfig.HudAnchorY.Value);
            _panel.sizeDelta = new Vector2(PanelWidth, height);
            _panel.anchoredPosition = new Vector2(0, (1f - ay) * (PanelHeight - height));
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
            float remaining = _endTime - Time.time;
            _pausedPulse = remaining <= 10f && remaining > 0f;   // keep beating if we froze in the last 10s
            _paused = true;
            _autoHideTime = Time.time + Mathf.Max(1f, ModConfig.ResultDisplayDuration.Value);
            SetVisible(true);
        }

        public void ShowResult(int winnerIndex, int winnerCount)
        {
            EnsureGameFont();
            _finished = true;
            _paused = false;
            _pausedPulse = false;
            _winnerIndex = winnerIndex;
            _winnerCount = winnerCount;
            _instruction.text = Loc.Get("panel.finished");

            // Self-contained timer: the result panel clears on its own after the
            // configured duration, even if the host's "hide" network message is lost.
            _autoHideTime = Time.time + Mathf.Max(1f, ModConfig.ResultDisplayDuration.Value);
            _votingHardDeadline = float.MaxValue;
            _active = true;

            // The result view has no countdown: hide the clock and reset the pulse.
            if (_clockIcon != null) _clockIcon.gameObject.SetActive(false);
            ApplyTimerVisual(1f, HeaderText);

            SetPanelHeight(ResultHeight);   // compact: header + the winner only
            SetVisible(true);
            Refresh();
        }

        public void Hide()
        {
            _active = false;
            _finished = false;
            _paused = false;
            _pausedPulse = false;
            _autoHideTime = float.MaxValue;
            _votingHardDeadline = float.MaxValue;
            if (_clockIcon != null) _clockIcon.gameObject.SetActive(false);
            ApplyTimerVisual(1f, HeaderText);
            SetVisible(false);
        }

        // ----------------------------------------------------------------- per-frame

        private void Update()
        {
            if (!_active) return;

            if (_paused)
            {
                // Frozen panel (ship left mid-poll): the number stays put and the counts
                // stay frozen, but if we froze in the last 10 seconds it keeps "beating"
                // (black<->red + grow/shrink) on a loop until the panel clears. Cancelled.
                if (_pausedPulse)
                {
                    float p = Mathf.Sin((Time.time % 1f) * Mathf.PI);   // one beat per second
                    ApplyTimerVisual(Mathf.Lerp(1f, 1.6f, p), Color.Lerp(HeaderText, AlarmRed, p));
                }
                if (Time.time >= _autoHideTime) Hide();
                return;
            }

            if (!_finished)
            {
                float remaining = _endTime - Time.time;
                int secs = Mathf.Max(0, Mathf.CeilToInt(remaining));
                _timer.text = secs + "s";
                UpdateTimerPulse(remaining);

                // Safety net: a voting panel must never stay stuck. If neither a result
                // nor a hide/pause arrived well past the vote end (e.g. a missed message),
                // clear it ourselves.
                if (Time.time >= _votingHardDeadline) Hide();
            }
            else
            {
                _timer.text = "";
                // Result view clears itself after ResultDisplayDuration (set in ShowResult),
                // so the panel always disappears completely on its own.
                if (Time.time >= _autoHideTime) Hide();
            }
        }

        /// <summary>
        /// During the last 10 seconds, the number and the clock icon "beat" once per
        /// second: they grow and turn red at the peak, then shrink back to normal size
        /// and black at the edges of each second. Above 10s they stay black and normal.
        /// </summary>
        private void UpdateTimerPulse(float remaining)
        {
            if (remaining <= 10f && remaining > 0f)
            {
                float intoSecond = Mathf.Ceil(remaining) - remaining; // 0..1 across the shown second
                float p = Mathf.Sin(intoSecond * Mathf.PI);           // 0 at the edges, 1 mid-second
                ApplyTimerVisual(Mathf.Lerp(1f, 1.6f, p), Color.Lerp(HeaderText, AlarmRed, p));
            }
            else
            {
                ApplyTimerVisual(1f, HeaderText);
            }
        }

        private void ApplyTimerVisual(float scale, Color color)
        {
            var s = new Vector3(scale, scale, 1f);
            _timer.rectTransform.localScale = s;
            _timer.color = color;
            if (_clockIcon != null)
            {
                _clockIcon.rectTransform.localScale = s;
                _clockIcon.color = color;
            }
        }

        // ----------------------------------------------------------------- rendering

        private void Refresh()
        {
            if (_finished) { RefreshResult(); return; }
            RefreshVoting();
        }

        /// <summary>Voting view: the 3 options with their live bars and counts.</summary>
        private void RefreshVoting()
        {
            int max = 1;
            for (int i = 0; i < _rowsUsed; i++) max = Mathf.Max(max, _counts[i]);

            float innerWidth = PanelWidth - Pad * 2;
            for (int i = 0; i < MaxRows; i++)
            {
                bool used = i < _rowsUsed;
                _rowBg[i].gameObject.SetActive(used);
                if (!used) continue;

                float frac = Mathf.Clamp01((float)_counts[i] / max);
                ((RectTransform)_rowFill[i].transform).sizeDelta = new Vector2(innerWidth * frac, RowHeight);
                _rowFill[i].color = BarOrange;
                _rowFill[i].gameObject.SetActive(_counts[i] > 0);

                _rowLabel[i].text = $"{i + 1} | {_labels[i]}";
                _rowLabel[i].color = RowText;
                _rowCount[i].text = _counts[i].ToString();
                _rowCount[i].color = RowText;
            }

            // No trophy during voting; keep the count flush right.
            _trophyIcon.gameObject.SetActive(false);
            _rowCount[0].rectTransform.anchoredPosition = new Vector2(-12, 0);
        }

        /// <summary>Result view: only the WINNER, shown alone in the first row (green + trophy).</summary>
        private void RefreshResult()
        {
            _rowBg[0].gameObject.SetActive(true);
            _rowBg[1].gameObject.SetActive(false);
            _rowBg[2].gameObject.SetActive(false);

            ((RectTransform)_rowFill[0].transform).sizeDelta = new Vector2(PanelWidth - Pad * 2, RowHeight);
            _rowFill[0].color = BarWinner;
            _rowFill[0].gameObject.SetActive(true);

            string label = (_winnerIndex >= 0 && _winnerIndex < _rowsUsed) ? _labels[_winnerIndex] : "";
            _rowLabel[0].text = label;                 // winner only, no number
            _rowLabel[0].color = HeaderText;           // dark text reads better on the green bar
            _rowCount[0].text = _winnerCount.ToString();
            _rowCount[0].color = HeaderText;

            // Drawn gold trophy at the far right of the row; shift the count left for room.
            _trophyIcon.color = Gold;
            _trophyIcon.gameObject.SetActive(true);
            _rowCount[0].rectTransform.anchoredPosition = new Vector2(-50, 0);
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
            // No GraphicRaycaster on purpose: this HUD is display-only and must NOT capture
            // mouse input (a raycaster here breaks the menu/game cursor).

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
            _title = NewText(_panel, "Title", ">SONDAGE", 38, TextAlignmentOptions.TopLeft, HeaderText, FontStyles.Bold);
            Place(_title.rectTransform, Pad, Pad, PanelWidth - Pad * 2, TitleH);

            // Timer (top-right): a drawn clock icon + the seconds number. Both pulse
            // (scale + colour) during the last 10 seconds.
            int titleMid = Pad + TitleH / 2;
            _clockIcon = NewImage(_panel, "ClockIcon", HeaderText);
            _clockIcon.sprite = MakeClockSprite();
            var iconRt = (RectTransform)_clockIcon.transform;
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(1, 1);
            iconRt.pivot = new Vector2(1, 0.5f);
            iconRt.sizeDelta = new Vector2(32, 32);
            iconRt.anchoredPosition = new Vector2(-(Pad + 78), -titleMid);

            _timer = NewText(_panel, "Timer", "", 32, TextAlignmentOptions.MidlineRight, HeaderText, FontStyles.Bold);
            var trt = _timer.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(1, 1);
            trt.pivot = new Vector2(1, 0.5f);
            trt.sizeDelta = new Vector2(72, TitleH);
            trt.anchoredPosition = new Vector2(-Pad, -titleMid);

            // Instruction line.
            _instruction = NewText(_panel, "Instruction", "", 22, TextAlignmentOptions.TopLeft, HeaderText, FontStyles.Normal);
            Place(_instruction.rectTransform, Pad, InstrY, PanelWidth - Pad * 2, InstrH);

            // Rows.
            int top = RowsTop;
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

                var label = NewText(bg.transform, "RowLabel" + i, "", 24, TextAlignmentOptions.MidlineLeft, RowText, FontStyles.Bold);
                Place(label.rectTransform, 12, 0, PanelWidth - Pad * 2 - 80, RowHeight);
                label.rectTransform.anchoredPosition = new Vector2(12, 0);
                StretchVert(label.rectTransform);

                var count = NewText(bg.transform, "RowCount" + i, "", 24, TextAlignmentOptions.MidlineRight, RowText, FontStyles.Bold);
                var crt = count.rectTransform;
                crt.anchorMin = new Vector2(1, 0); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(1, 0.5f);
                crt.sizeDelta = new Vector2(100, 0);
                crt.anchoredPosition = new Vector2(-12, 0);
                _rowLabel[i] = label;
                _rowCount[i] = count;
            }

            // Trophy icon for the winner row (drawn sprite; the game's pixel font has no
            // glyph for the 🏆 emoji). Lives at the far right of the winner row, hidden
            // until the result view is shown.
            _trophyIcon = NewImage(_rowBg[0].transform, "TrophyIcon", HeaderText);
            _trophyIcon.sprite = MakeTrophySprite();
            var thr = (RectTransform)_trophyIcon.transform;
            thr.anchorMin = thr.anchorMax = new Vector2(1, 0.5f);
            thr.pivot = new Vector2(1, 0.5f);
            thr.sizeDelta = new Vector2(32, 32);
            thr.anchoredPosition = new Vector2(-10, 0);
            _trophyIcon.gameObject.SetActive(false);
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

        private TextMeshProUGUI NewText(Transform parent, string name, string text, int size,
                                        TextAlignmentOptions align, Color color, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text;
            if (_gameFont != null) t.font = _gameFont;   // else applied lazily by EnsureGameFont()
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = color;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Applies the game's TMP font to all texts (lazily, once it's available).</summary>
        private void EnsureGameFont()
        {
            if (_fontApplied) return;
            var f = FindGameFont();
            if (f == null) return;

            _gameFont = f;
            _title.font = f; _timer.font = f; _instruction.font = f;
            for (int i = 0; i < MaxRows; i++) { _rowLabel[i].font = f; _rowCount[i].font = f; }
            _fontApplied = true;
        }

        private static TMP_FontAsset? FindGameFont() => GameFont.Find();

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


        private static Sprite MakeWhiteSprite()
        {
            var tex = Texture2D.whiteTexture;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// Draws a simple stopwatch icon (ring + top button + two hands) into a texture
        /// at runtime. White on transparent, so it can be tinted via Image.color (black
        /// normally, red on the pulse).
        /// </summary>
        private static Sprite MakeClockSprite()
        {
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[S * S];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 0);

            float cx = 31.5f, cy = 28f, R = 21f, thick = 4.5f;
            var on = new Color32(255, 255, 255, 255);

            void Set(int x, int y) { if (x >= 0 && x < S && y >= 0 && y < S) px[y * S + x] = on; }

            void Line(float x0, float y0, float x1, float y1, float w)
            {
                float steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) * 2f + 1f;
                int hw = Mathf.CeilToInt(w / 2f);
                for (int s = 0; s <= steps; s++)
                {
                    float t = s / steps;
                    int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                    int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                    for (int oy = -hw; oy <= hw; oy++)
                        for (int ox = -hw; ox <= hw; ox++) Set(x + ox, y + oy);
                }
            }

            void FillRect(int x0, int x1, int y0, int y1)
            {
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Set(x, y);
            }

            // Ring (clock body).
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x - cx, dy = y - cy, d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= R && d >= R - thick) Set(x, y);
                }

            // Stopwatch top button (stem + cap) above the ring.
            FillRect((int)cx - 2, (int)cx + 1, (int)(cy + R), (int)(cy + R) + 4);
            FillRect((int)cx - 4, (int)cx + 3, (int)(cy + R) + 4, (int)(cy + R) + 6);

            // Hands: one up, one up-right, plus a small centre hub.
            Line(cx, cy, cx, cy + R * 0.55f, 2.2f);
            Line(cx, cy, cx + R * 0.62f, cy + R * 0.20f, 2.2f);
            Line(cx, cy, cx, cy, 3f);

            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// Draws a trophy cup (bowl + side handles + stem + base) into a texture at runtime.
        /// White on transparent, tinted via Image.color. y is up (0 = bottom of the texture).
        /// </summary>
        private static Sprite MakeTrophySprite()
        {
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[S * S];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 0);

            var on = new Color32(255, 255, 255, 255);
            float cx = 31.5f;

            void Set(int x, int y) { if (x >= 0 && x < S && y >= 0 && y < S) px[y * S + x] = on; }
            void FillRect(int x0, int x1, int y0, int y1)
            {
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Set(x, y);
            }

            // Cup bowl: solid, tapering from a wide rim (top) down to a rounded bottom.
            const int bowlBottom = 30, bowlTop = 53;
            for (int y = bowlBottom; y <= bowlTop; y++)
            {
                float t = (float)(y - bowlBottom) / (bowlTop - bowlBottom);   // 0 bottom .. 1 rim
                float halfW = Mathf.Lerp(7f, 16f, t);
                FillRect(Mathf.RoundToInt(cx - halfW), Mathf.RoundToInt(cx + halfW), y, y);
            }
            // Rim lip: a slightly wider bar across the very top.
            FillRect(Mathf.RoundToInt(cx - 17), Mathf.RoundToInt(cx + 17), 53, 56);

            // Handles: a ring on each side of the bowl, keeping only the outer half.
            void Handle(float hcx, bool left)
            {
                const float r = 8.5f, thick = 3f;
                for (int y = 38; y <= 54; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float dx = x - hcx, dy = y - 47f, d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d > r || d < r - thick) continue;
                        if (left ? x <= hcx : x >= hcx) Set(x, y);
                    }
            }
            Handle(cx - 15f, true);
            Handle(cx + 15f, false);

            // Stem under the bowl, a knob, then a wide base plate.
            FillRect(Mathf.RoundToInt(cx - 2.5f), Mathf.RoundToInt(cx + 2.5f), 21, 31);
            FillRect(Mathf.RoundToInt(cx - 5f), Mathf.RoundToInt(cx + 5f), 17, 21);
            FillRect(Mathf.RoundToInt(cx - 12f), Mathf.RoundToInt(cx + 12f), 9, 15);

            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
