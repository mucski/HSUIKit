using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HSUIKit
{
    // Floating window: drop shadow, glass body, draggable title bar, animated open/close,
    // optional edge/corner resizing, double-click (or chevron) collapse to the title bar, and
    // optional position/size memory across sessions via Remember(id).
    //
    // Deliberately NOT an IPointerDownHandler: UGUI routes a click to whichever object got the
    // pointer-down, found by walking UP the hierarchy. A down handler on this root therefore
    // stole the press (and so the click) from every click-only child - dropdowns and checkboxes
    // were dead. Bring-to-front is a mouse-position check instead.
    public class KitWindow : KitElement
    {
        public const float TitleHeight = 44f;
        private static int _visibleCount;

        public static int VisibleCount { get { return _visibleCount; } }

        public RectTransform Rect;
        public RectTransform Content;
        public event BoolHandler VisibilityChanged;

        private CanvasGroup _group;
        private CanvasGroup _contentGroup;
        private Image _shadow;
        private Image _body;
        private Image _border;
        private Image _divider;
        private Text _title;
        private Image _barFill, _barSquare;
        private RectTransform _bar;
        private int _barChildren = -1;
        private float _titleInset = 84f;   // right edge of the free title space, measured from the bar's right
        private KitIconButton _collapseButton;
        private bool _shown;

        private RectTransform _grips;
        private Image _cornerGlyph;
        private Vector2 _minSize = new Vector2(280f, 160f);
        private Vector2 _maxSize = new Vector2(4000f, 4000f);
        private bool _collapsed;
        private float _expandedHeight;
        private string _rememberId;

        public bool Visible { get { return _shown; } }
        public bool Collapsed { get { return _collapsed; } }

        public string Title
        {
            get { return _title.text; }
            set { _title.text = value; }
        }

        public static KitWindow Create(string title, float width, float height)
        {
            Kit.Ensure();
            RectTransform rt = Kit.Rect(Kit.WindowLayer, "KitWindow " + title);
            Kit.Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));

            // NO nested Canvas here (tried and reverted): on Unity 5.3,
            // RectMask2D computes its clip rect in the ROOT canvas's space but a nested canvas's
            // graphics apply it in their own space, so every scroll view clipped at an offset -
            // tops of all tabs vanished. Keep windows on the shared root canvas.

            KitWindow w = rt.gameObject.AddComponent<KitWindow>();
            w.Rect = rt;
            w._group = rt.gameObject.AddComponent<CanvasGroup>();
            w._expandedHeight = height;

            w._shadow = Kit.Image(rt, "Shadow", null, Color.clear);
            w._body = Kit.Image(rt, "Body", null, Color.clear, true);
            Kit.Fill(w._body.rectTransform);

            RectTransform bar = w._bar = Kit.Rect(rt, "TitleBar");
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(0f, TitleHeight);
            bar.anchoredPosition = Vector2.zero;
            Kit.HitArea(bar.gameObject);
            KitDragHandle drag = bar.gameObject.AddComponent<KitDragHandle>();
            drag.Window = w;

            // Title bar fill (Candy themes): rounded on top, plus a square strip over its lower half so it
            // meets the content with straight corners. Transparent in Glass Dark.
            w._barFill = Kit.Image(bar, "BarFill", null, Color.clear);
            Kit.Fill(w._barFill.rectTransform);
            w._barSquare = Kit.Image(bar, "BarSquare", null, Color.clear);
            RectTransform sq = w._barSquare.rectTransform;
            sq.anchorMin = new Vector2(0f, 0f); sq.anchorMax = new Vector2(1f, 0.5f);
            sq.offsetMin = Vector2.zero; sq.offsetMax = Vector2.zero;

            w._title = Kit.MakeText(bar, "Title", title, Theme.TitleSize, Theme.Text, TextAnchor.MiddleLeft);
            Kit.Fill(w._title.rectTransform, 18f, 0f, 84f, 0f);

            KitIconButton close = KitIconButton.Create(bar, Sprites.Cross, 28f, true, w.Hide);
            close.OnTitleBar = true;
            Kit.Anchor(close.Rect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(28f, 28f));
            w._collapseButton = KitIconButton.Create(bar, Sprites.Chevron, 28f, false, delegate { w.SetCollapsed(!w._collapsed, true); });
            Kit.Anchor(w._collapseButton.Rect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-42f, 0f), new Vector2(28f, 28f));
            w._collapseButton.OnTitleBar = true;
            w._collapseButton.Icon.transform.localEulerAngles = new Vector3(0f, 0f, 180f); // points up = "collapse"
            KitTooltip.Attach(w._collapseButton.gameObject, "Collapse (or double-click the title bar)");

            w._divider = Kit.Image(rt, "Divider", null, Color.clear);
            RectTransform d = w._divider.rectTransform;
            d.anchorMin = new Vector2(0f, 1f);
            d.anchorMax = new Vector2(1f, 1f);
            d.pivot = new Vector2(0.5f, 1f);
            d.sizeDelta = new Vector2(0f, 1f);
            d.anchoredPosition = new Vector2(0f, -TitleHeight);

            w.Content = Kit.Rect(rt, "Content");
            Kit.Fill(w.Content, 0f, 0f, 0f, TitleHeight + 1f);
            w._contentGroup = w.Content.gameObject.AddComponent<CanvasGroup>();

            w._border = Kit.Image(rt, "Border", null, Color.clear);
            Kit.Fill(w._border.rectTransform);

            w.Finish();
            rt.gameObject.SetActive(false);
            return w;
        }

        protected override void ApplyTheme()
        {
            int s = Theme.ShadowSize;
            Kit.SetSprite(_shadow, Sprites.Shadow(Theme.Radius, Mathf.Max(1, s)));
            _shadow.color = new Color(0f, 0f, 0f, s > 0 ? Theme.ShadowAlpha : 0f);
            float drop = s * 0.35f;
            Kit.Fill(_shadow.rectTransform, -s, -s - drop, -s, -s + drop);

            Kit.SetSprite(_body, Sprites.Rounded(Theme.Radius));
            _body.color = Theme.Background;
            Kit.SetSprite(_border, Sprites.Outline(Theme.Radius, 1));
            _border.color = Theme.Border;
            _divider.sprite = null;
            _divider.color = Theme.Border;

            bool coloured = Theme.TitleBar.a > 0.01f;
            Kit.SetSprite(_barFill, Sprites.Rounded(Theme.Radius));
            _barFill.color = Theme.TitleBar;
            _barSquare.sprite = null;
            _barSquare.color = Theme.TitleBar;
            UpdateBarParts(_collapsed);

            _title.font = Kit.Font;
            _title.fontSize = Theme.TitleSize;
            _title.fontStyle = FontStyle.Bold;
            _title.color = coloured ? Theme.TitleText : Theme.Text;
            PlaceTitle();

            if (_cornerGlyph != null) _cornerGlyph.color = Kit.Alpha(Theme.TextDim, 0.45f);
        }

        // ---------------------------------------------------------------- visibility

        public void Toggle()
        {
            if (_shown) Hide();
            else Show();
        }

        public void Show()
        {
            if (!_shown)
            {
                _shown = true;
                _visibleCount++;
            }
            gameObject.SetActive(true);
            BringToFront();
            _group.blocksRaycasts = true;
            _group.interactable = true;
            transform.localScale = Vector3.one;
            if (_group.alpha <= 0.001f) Tween.SlideIn(Rect, new Vector2(0f, -10f), 0.24f, Ease.OutCubic);
            Tween.Fade(_group, 1f, 0.18f, Ease.OutCubic);
            Raise(true);
        }

        public void Hide()
        {
            if (!_shown) return;
            _shown = false;
            _visibleCount--;
            KitPopup.CloseAll();
            KitTooltip.HideNow();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            Tween.Fade(_group, 0f, 0.14f, Ease.InQuad, delegate { gameObject.SetActive(false); });
            Raise(false);
        }

        // Only reorders when another window is actually above: a sibling change marks the canvas
        // hierarchy changed (every graphic re-registers with its clipping mask), which was being
        // paid on every click on an already-front window - twice (title bar + window Update).
        public void BringToFront()
        {
            Transform parent = transform.parent;
            if (parent != null && transform.GetSiblingIndex() == parent.childCount - 1) return;
            transform.SetAsLastSibling();
        }

        private void Update()
        {
            if (!_shown || !Input.GetMouseButtonDown(0)) return;
            Vector2 mouse = Input.mousePosition;
            if (!RectTransformUtility.RectangleContainsScreenPoint(Rect, mouse, null)) return;
            // Only the topmost window under the cursor comes forward.
            Transform layer = transform.parent;
            for (int i = transform.GetSiblingIndex() + 1; i < layer.childCount; i++)
            {
                RectTransform other = layer.GetChild(i) as RectTransform;
                if (other != null && other.gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(other, mouse, null)) return;
            }
            BringToFront();
        }

        // Keeps the window's corner on the screen-pixel grid. At fractional positions (drag deltas /
        // scale factor, a centred pivot with an odd width, restored state) every 9-slice seam lands
        // between pixels and shows as faint dashed lines, and edges go soft. Only runs when moved.
        private Vector3 _snappedPos = new Vector3(float.NaN, 0f, 0f);
        private Vector2 _snappedSize;
        private float _snappedScale;

        private void LateUpdate()
        {
            if (_fitContent) FitContentNow();
            if (_bar != null && _bar.childCount != _barChildren) ScanTitleBar();
            else if (_title != null && (!ReferenceEquals(_title.text, _placedText) || _bar.rect.width != _placedWidth)) PlaceTitle();
            Vector3 p = Rect.position;
            Vector2 size = Rect.rect.size;
            float scale = Rect.lossyScale.x;
            if (p == _snappedPos && size == _snappedSize && scale == _snappedScale) return;
            if (transform.localScale != Vector3.one) return;   // mid open/close animation: snapping each frame wobbled the window
            if (scale > 0.0001f)
            {
                Vector2 min = Rect.rect.min;
                float cx = p.x + min.x * scale, cy = p.y + min.y * scale;
                float dx = Mathf.Round(cx) - cx, dy = Mathf.Round(cy) - cy;
                if (Mathf.Abs(dx) > 0.001f || Mathf.Abs(dy) > 0.001f)
                {
                    p = new Vector3(p.x + dx, p.y + dy, p.z);
                    Rect.position = p;
                }
            }
            _snappedPos = p;
            _snappedSize = size;
            _snappedScale = scale;
        }

        private void Raise(bool visible)
        {
            if (VisibilityChanged == null) return;
            try { VisibilityChanged(visible); }
            catch (System.Exception ex) { Log.Write("KitWindow.VisibilityChanged threw: " + ex); }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            // Deactivated from outside (scene teardown etc.) while logically shown.
            if (_shown && !gameObject.activeSelf)
            {
                _shown = false;
                _visibleCount--;
            }
        }

        private void OnDestroy()
        {
            if (_shown) _visibleCount--;
            _shown = false;
        }

        // ---------------------------------------------------------------- resize

        // Enables dragging any edge or corner. Content re-flows through the layout groups.
        // Adds an icon button to the title bar, left of collapse/close (and of earlier added ones). Styled for
        // the bar; the title keeps clear of it.
        public KitIconButton AddTitleButton(Sprite icon, string tooltip, VoidHandler onClick)
        {
            int extra = -2;   // close + collapse are built in
            for (int i = 0; i < _bar.childCount; i++) if (_bar.GetChild(i).GetComponent<KitIconButton>() != null) extra++;
            KitIconButton btn = KitIconButton.Create(_bar, icon, 28f, false, onClick);
            Kit.Anchor(btn.Rect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-74f - 32f * Mathf.Max(0, extra), 0f), new Vector2(28f, 28f));
            if (!string.IsNullOrEmpty(tooltip)) KitTooltip.Attach(btn.gameObject, tooltip);
            ScanTitleBar();
            return btn;
        }

        // Every icon button on the bar - including ones a plugin parented there itself - gets title-bar
        // styling, and the title text stays clear of the leftmost one (centred themes keep it symmetric).
        // Runs whenever the bar's children change, so older plugins that add buttons directly are covered.
        private void ScanTitleBar()
        {
            _barChildren = _bar.childCount;
            float barRight = _bar.rect.xMax, leftmost = barRight - 84f + 10f;
            for (int i = 0; i < _bar.childCount; i++)
            {
                KitIconButton b = _bar.GetChild(i).GetComponent<KitIconButton>();
                if (b == null) continue;
                b.OnTitleBar = true;
                RectTransform r = b.Rect;
                float xMin = r.localPosition.x + r.rect.xMin;
                if (b.gameObject.activeSelf && xMin < leftmost) leftmost = xMin;
            }
            _titleInset = Mathf.Max(84f, barRight - leftmost + 10f);
            PlaceTitle();
        }

        // Centred themes centre the title when it fits between symmetric insets; when the bar is crowded
        // (HSPE: five icons in a narrow window) it falls back to left-aligned in the free space.
        private string _placedText;
        private float _placedWidth = -1f;

        private void PlaceTitle()
        {
            if (_title == null) return;
            _placedText = _title.text;
            _placedWidth = _bar.rect.width;
            bool centre = Theme.TitleCentered && _bar.rect.width - 2f * _titleInset >= _title.preferredWidth;
            _title.alignment = centre ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            Kit.Fill(_title.rectTransform, centre ? _titleInset : 18f, 0f, _titleInset, 0f);
        }

        public KitWindow Resizable(Vector2 minSize, Vector2 maxSize)
        {
            _minSize = minSize;
            _maxSize = maxSize;
            if (_grips != null) return this;

            _grips = Kit.Rect(Rect, "ResizeGrips");
            Kit.Fill(_grips);
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    KitResizeGrip.Create(this, _grips, dx, dy);
                }
            }
            _cornerGlyph = Kit.Image(Rect, "CornerGlyph", Sprites.ResizeGrip, Kit.Alpha(Theme.TextDim, 0.45f));
            Kit.Anchor(_cornerGlyph.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-3f, 3f), new Vector2(16f, 16f));
            _grips.SetAsLastSibling();
            return this;
        }

        internal Vector2 ClampSize(Vector2 size)
        {
            Rect layer = Kit.WindowLayer.rect;
            float maxW = Mathf.Min(_maxSize.x, Mathf.Max(_minSize.x, layer.width));
            float maxH = Mathf.Min(_maxSize.y, Mathf.Max(_minSize.y, layer.height));
            return new Vector2(Mathf.Clamp(size.x, _minSize.x, maxW), Mathf.Clamp(size.y, _minSize.y, maxH));
        }

        // Keeps the title bar reachable: never above the screen top, never fully off a side/bottom.
        internal Vector2 ClampPosition(Vector2 p, Vector2 size)
        {
            Rect layer = Kit.WindowLayer.rect;
            float maxX = layer.width * 0.5f + size.x * 0.5f - 80f;
            p.x = Mathf.Clamp(p.x, -maxX, maxX);
            float maxY = layer.height * 0.5f - size.y * 0.5f;
            float minY = -layer.height * 0.5f + TitleHeight - size.y * 0.5f;
            p.y = Mathf.Clamp(p.y, minY, maxY);
            return p;
        }

        // ---------------------------------------------------------------- collapse

        public void SetCollapsed(bool collapsed, bool animate)
        {
            if (collapsed == _collapsed) return;
            _collapsed = collapsed;
            if (!collapsed) UpdateBarParts(false);   // collapsing updates when the animation ends
            KitPopup.CloseAll();
            KitTooltip.HideNow();

            float from = Rect.sizeDelta.y;
            float to;
            if (collapsed)
            {
                _expandedHeight = from;
                to = TitleHeight;
            }
            else
            {
                to = _expandedHeight;
                Content.gameObject.SetActive(true);
            }
            if (_grips != null) _grips.gameObject.SetActive(!collapsed);
            if (_cornerGlyph != null) _cornerGlyph.enabled = !collapsed;
            Tween.Rotate(_collapseButton.Icon.transform, collapsed ? 0f : 180f, animate ? 0.3f : 0f, Ease.OutBack);

            if (!animate)
            {
                SetHeightKeepTop(to);
                _contentGroup.alpha = collapsed ? 0f : 1f;
                Content.gameObject.SetActive(!collapsed);
                UpdateBarParts(collapsed);
                SaveState();
                return;
            }

            if (collapsed)
            {
                Tween.Fade(_contentGroup, 0f, 0.1f, Ease.OutQuad);
            }
            else
            {
                _contentGroup.alpha = 0f;
                Tween.Fade(_contentGroup, 1f, 0.22f, Ease.OutQuad, null, 0.12f);
            }
            Tween.Run(this, "collapse", collapsed ? 0.26f : 0.34f, collapsed ? Ease.InOutCubic : Ease.OutCubic,
                delegate(float t) { SetHeightKeepTop(Mathf.Max(TitleHeight, Tween.Lerp(from, to, t))); },
                delegate
                {
                    if (_collapsed) { Content.gameObject.SetActive(false); UpdateBarParts(true); }
                    SaveState();
                });
        }

        // Coloured title bar (Candy themes): the body starts halfway down the bar instead of under all of
        // it - two identical anti-aliased rounded shapes stacked let the light body's edge fringe show
        // around the bar's corners as a pale, jaggy-looking rim. Collapsed, the bar alone is the window.
        private void UpdateBarParts(bool collapsed)
        {
            bool coloured = Theme.TitleBar.a > 0.01f;
            _barSquare.enabled = coloured && !collapsed;   // square bottom only while content shows
            _divider.enabled = !coloured && !collapsed;    // a coloured bar is its own separator
            Kit.Fill(_body.rectTransform, 0f, 0f, 0f, coloured ? TitleHeight * 0.5f : 0f);
            _body.enabled = !(coloured && collapsed);
        }

        // Height follows the content: for hand-built windows whose Content has a vertical layout group (settings
        // panels). Without it a fixed height left dead space under short content (Timeline Settings).
        public KitWindow FitToContent()
        {
            _fitContent = true;
            return this;
        }

        private bool _fitContent;

        private void FitContentNow()
        {
            if (!_fitContent || _collapsed || !_shown) return;
            float want = Mathf.Ceil(LayoutUtility.GetPreferredHeight(Content) + TitleHeight + 1f);
            if (want > TitleHeight + 2f && Mathf.Abs(want - Rect.sizeDelta.y) > 0.5f)
            {
                SetHeightKeepTop(want);
                _expandedHeight = want;
            }
        }

        // Shrinks the (expanded) window so its body just holds `contentHeight`; never grows it. For forms whose
        // content is shorter than the size they asked for.
        public void ShrinkToContent(float contentHeight)
        {
            if (_collapsed || contentHeight <= 0f) return;
            float want = Mathf.Ceil(contentHeight + TitleHeight + 1f);
            if (_grips != null) want = Mathf.Max(want, _minSize.y);
            if (want >= Rect.sizeDelta.y - 1f) return;
            SetHeightKeepTop(want);
            _expandedHeight = want;
        }

        private void SetHeightKeepTop(float h)
        {
            Vector2 size = Rect.sizeDelta;
            Vector2 pos = Rect.anchoredPosition;
            pos.y -= (h - size.y) * 0.5f;
            size.y = h;
            Rect.sizeDelta = size;
            Rect.anchoredPosition = pos;
        }

        // ---------------------------------------------------------------- persistence

        // Restores this window's last position/size/collapsed state (if any) and saves it
        // whenever a drag, resize or collapse finishes. Call after Resizable().
        public KitWindow Remember(string id)
        {
            _rememberId = id;
            float x, y, w, h;
            bool collapsed;
            if (KitWindowState.TryGet(id, out x, out y, out w, out h, out collapsed))
            {
                Vector2 size = _grips != null ? ClampSize(new Vector2(w, h)) : Rect.sizeDelta;
                Rect.sizeDelta = size;
                _expandedHeight = size.y;
                Rect.anchoredPosition = ClampPosition(new Vector2(x, y), size);
                if (collapsed) SetCollapsed(true, false);
            }
            return this;
        }

        internal void SaveState()
        {
            if (string.IsNullOrEmpty(_rememberId)) return;
            Vector2 p = Rect.anchoredPosition;
            Vector2 s = Rect.sizeDelta;
            float h = _collapsed ? _expandedHeight : s.y;
            // Stored position is for the EXPANDED window (top edge kept), so restore is simple.
            float y = _collapsed ? p.y - (h - s.y) * 0.5f : p.y;
            KitWindowState.Set(_rememberId, p.x, y, s.x, h, _collapsed);
        }

        internal void BeginResizeVisual(bool on)
        {
            Tween.Tint(_border, on ? Kit.Alpha(Theme.Accent, 0.7f) : Theme.Border, 0.15f, Ease.OutQuad);
        }
    }

    // Plugins/HSUIKit/windows.txt: one "id = x,y,w,h,collapsed" line per remembered window.
    internal static class KitWindowState
    {
        private const string FilePath = "Plugins/HSUIKit/windows.txt";
        private static Dictionary<string, string> _entries;

        private static void Load()
        {
            if (_entries != null) return;
            _entries = new Dictionary<string, string>();
            try
            {
                if (!File.Exists(FilePath)) return;
                string[] lines = File.ReadAllLines(FilePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    int eq = lines[i].IndexOf('=');
                    if (eq <= 0) continue;
                    _entries[lines[i].Substring(0, eq).Trim()] = lines[i].Substring(eq + 1).Trim();
                }
            }
            catch (System.Exception ex) { Log.Write("KitWindowState.Load failed: " + ex.Message); }
        }

        public static bool TryGet(string id, out float x, out float y, out float w, out float h, out bool collapsed)
        {
            x = y = w = h = 0f;
            collapsed = false;
            Load();
            string v;
            if (!_entries.TryGetValue(id, out v)) return false;
            string[] p = v.Split(',');
            if (p.Length < 5) return false;
            CultureInfo ci = CultureInfo.InvariantCulture;
            return float.TryParse(p[0], NumberStyles.Float, ci, out x) && float.TryParse(p[1], NumberStyles.Float, ci, out y) &&
                   float.TryParse(p[2], NumberStyles.Float, ci, out w) && float.TryParse(p[3], NumberStyles.Float, ci, out h) &&
                   bool.TryParse(p[4], out collapsed);
        }

        public static void Set(string id, float x, float y, float w, float h, bool collapsed)
        {
            Load();
            CultureInfo ci = CultureInfo.InvariantCulture;
            _entries[id] = string.Format(ci, "{0:0.#},{1:0.#},{2:0.#},{3:0.#},{4}", x, y, w, h, collapsed);
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# HSUIKit window positions/sizes - safe to delete.");
                foreach (KeyValuePair<string, string> kv in _entries) sb.AppendLine(kv.Key + " = " + kv.Value);
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, sb.ToString());
            }
            catch (System.Exception ex) { Log.Write("KitWindowState.Set failed: " + ex.Message); }
        }
    }

    public class KitDragHandle : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public KitWindow Window;

        public void OnPointerDown(PointerEventData e)
        {
            Window.BringToFront();
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && e.clickCount == 2)
            {
                Window.SetCollapsed(!Window.Collapsed, true);
            }
        }

        public void OnBeginDrag(PointerEventData e)
        {
            KitPopup.CloseAll();
            KitTooltip.HideNow();
            Tween.Kill(Window.Rect, "slide");   // a still-running open slide would fight the drag
        }

        public void OnDrag(PointerEventData e)
        {
            RectTransform rt = Window.Rect;
            rt.anchoredPosition = Window.ClampPosition(rt.anchoredPosition + e.delta / Kit.ScaleFactor, rt.rect.size);
        }

        public void OnEndDrag(PointerEventData e)
        {
            Window.SaveState();
        }
    }

    // One of the 8 invisible edge/corner hit strips. Resizes from the drag's START state plus
    // the total pointer travel (not per-frame deltas), so hitting the min size and coming back
    // doesn't leave the edge drifting away from the cursor.
    public class KitResizeGrip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float Edge = 8f, Corner = 16f;

        private KitWindow _window;
        private int _dx, _dy;
        private Image _highlight;
        private Vector2 _startPointer, _startSize, _startPos;
        private bool _dragging, _hover;

        public static void Create(KitWindow window, RectTransform parent, int dx, int dy)
        {
            RectTransform rt = Kit.Rect(parent, "Grip " + dx + "," + dy);
            bool corner = dx != 0 && dy != 0;
            // Anchor to the edge/corner, straddling the border (half outside) for an easy grab.
            float ax = dx < 0 ? 0f : (dx > 0 ? 1f : 0.5f);
            float ay = dy < 0 ? 0f : (dy > 0 ? 1f : 0.5f);
            if (corner)
            {
                Kit.Anchor(rt, new Vector2(ax, ay), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Corner, Corner));
            }
            else if (dx != 0)
            {
                rt.anchorMin = new Vector2(ax, 0f);
                rt.anchorMax = new Vector2(ax, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(Edge, -Corner);
                rt.anchoredPosition = Vector2.zero;
            }
            else
            {
                rt.anchorMin = new Vector2(0f, ay);
                rt.anchorMax = new Vector2(1f, ay);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(-Corner, Edge);
                rt.anchoredPosition = Vector2.zero;
            }
            Kit.HitArea(rt.gameObject);

            KitResizeGrip g = rt.gameObject.AddComponent<KitResizeGrip>();
            g._window = window;
            g._dx = dx;
            g._dy = dy;

            // Accent line along the edge (or a dot on corners) that lights up on hover/drag.
            g._highlight = Kit.Image(rt, "Highlight", corner ? Sprites.Circle : Sprites.Pill(2f), Color.clear);
            RectTransform h = g._highlight.rectTransform;
            if (corner) Kit.Anchor(h, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 8f));
            else if (dx != 0) { Kit.Fill(h); h.sizeDelta = new Vector2(2f - Edge, -8f); }
            else { Kit.Fill(h); h.sizeDelta = new Vector2(-8f, 2f - Edge); }
        }

        private void Light()
        {
            bool on = _hover || _dragging;
            Tween.Tint(_highlight, on ? Kit.Alpha(Theme.Accent, 0.85f) : Kit.Alpha(Theme.Accent, 0f), 0.15f, Ease.OutQuad);
        }

        public void OnPointerEnter(PointerEventData e) { _hover = true; Light(); }

        public void OnPointerExit(PointerEventData e) { _hover = false; Light(); }

        public void OnPointerDown(PointerEventData e) { _window.BringToFront(); }

        public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold = false; }

        public void OnBeginDrag(PointerEventData e)
        {
            _dragging = true;
            Light();
            KitPopup.CloseAll();
            KitTooltip.HideNow();
            _startPointer = e.position;
            _startSize = _window.Rect.sizeDelta;
            _startPos = _window.Rect.anchoredPosition;
            _window.BeginResizeVisual(true);
        }

        public void OnDrag(PointerEventData e)
        {
            Vector2 travel = (e.position - _startPointer) / Kit.ScaleFactor;
            Vector2 wanted = _startSize + new Vector2(_dx * travel.x, _dy * travel.y);
            Vector2 size = _window.ClampSize(wanted);
            if (_dx == 0) size.x = _startSize.x;
            if (_dy == 0) size.y = _startSize.y;
            // Window pivot is its center: move it by half the growth toward the dragged side
            // so the opposite edge stays put.
            Vector2 pos = _startPos + new Vector2(_dx * (size.x - _startSize.x), _dy * (size.y - _startSize.y)) * 0.5f;
            _window.Rect.sizeDelta = size;
            _window.Rect.anchoredPosition = pos;
        }

        public void OnEndDrag(PointerEventData e)
        {
            _dragging = false;
            Light();
            _window.BeginResizeVisual(false);
            _window.SaveState();
        }
    }
}
