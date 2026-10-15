using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HSUIKit
{
    // Own dropdown, not UnityEngine.UI.Dropdown: the list is built in Kit.PopupLayer (outside
    // every window's scroll mask) at open time and destroyed on close, which structurally
    // avoids the clipping/positioning saga HSDebugUI had with the stock control.
    public class KitDropdown : KitElement, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
    {
        private const float FieldH = 34f, ItemH = 30f, MaxListH = 244f, SearchH = 32f;

        // Lists longer than this get a search box at the top of the popup (type to filter,
        // Enter picks the first match). Set per instance; 0 = never.
        public int SearchThreshold = 12;

        public IntHandler OnChanged;

        private readonly List<string> _options = new List<string>();
        private int _selected;
        private Image _bg, _outline, _chevron;
        private Text _label;
        private bool _hover, _open;

        private RectTransform _popup;
        private RectTransform _blocker;
        private VoidHandler _closeHandler;
        private KitScroll _listScroll;
        private CanvasGroup _popupGroup;
        private readonly List<int> _visible = new List<int>();

        public int Selected
        {
            get { return _selected; }
            set { Select(value, false); }
        }

        public string SelectedText
        {
            get { return _selected >= 0 && _selected < _options.Count ? _options[_selected] : ""; }
        }

        // Text shown in the closed field; normally the selected option. Setting it overrides the
        // display until the next selection (e.g. to show a value that isn't in the list).
        public string Caption
        {
            get { return _label.text; }
            set { _label.text = value ?? ""; }
        }

        public int OptionCount { get { return _options.Count; } }

        public int IndexOf(string option)
        {
            return _options.IndexOf(option);
        }

        public static KitDropdown Create(Transform parent, IList<string> options, int selected, IntHandler onChanged)
        {
            RectTransform rt = Kit.Rect(parent, "Dropdown");
            Kit.Layout(rt.gameObject, -1f, FieldH);
            KitDropdown d = rt.gameObject.AddComponent<KitDropdown>();
            d.OnChanged = onChanged;
            d._options.AddRange(options);
            d._selected = Mathf.Clamp(selected, 0, Mathf.Max(0, options.Count - 1));

            d._bg = rt.gameObject.AddComponent<Image>();
            d._bg.raycastTarget = true;
            d._label = Kit.MakeText(rt, "Label", d.SelectedText, Theme.FontSize, Theme.Text, TextAnchor.MiddleLeft);
            Kit.Fill(d._label.rectTransform, 14f, 0f, 36f, 0f);
            d._chevron = Kit.Image(rt, "Chevron", Sprites.Chevron, Color.white);
            Kit.Anchor(d._chevron.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-18f, 0f), new Vector2(18f, 18f));
            d._outline = Kit.Image(rt, "Outline", null, Color.clear);
            Kit.Fill(d._outline.rectTransform);
            d._closeHandler = d.Close;
            d.Finish();
            return d;
        }

        public void SetOptions(IList<string> options, int selected)
        {
            Close();
            if (!SameOptions(options))
            {
                _options.Clear();
                _options.AddRange(options);
            }
            _selected = Mathf.Clamp(selected, 0, Mathf.Max(0, options.Count - 1));
            _label.text = SelectedText;
        }

        private bool SameOptions(IList<string> options)
        {
            if (options.Count != _options.Count) return false;
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i] != _options[i]) return false;
            }
            return true;
        }

        protected override void ApplyTheme()
        {
            Kit.SetSprite(_bg, Sprites.Rounded(Theme.RadiusSmall));
            _bg.color = _hover || _open ? Theme.SurfaceHover : Theme.Surface;
            Kit.SetSprite(_outline, Sprites.Outline(Theme.RadiusSmall, 1));
            _outline.color = _open ? Kit.Alpha(Theme.Accent, 0.8f) : Theme.Border;
            _chevron.color = Theme.TextDim;
            _label.font = Kit.Font;
            _label.fontSize = Theme.FontSize;
            _label.color = Theme.Text;
        }

        private void RefreshField()
        {
            Tween.Tint(_bg, _hover || _open ? Theme.SurfaceHover : Theme.Surface, 0.15f, Ease.OutQuad);
            Tween.Tint(_outline, _open ? Kit.Alpha(Theme.Accent, 0.8f) : Theme.Border, 0.15f, Ease.OutQuad);
        }

        public void OnPointerEnter(PointerEventData e) { _hover = true; RefreshField(); }

        public void OnPointerExit(PointerEventData e) { _hover = false; RefreshField(); }

        // Claims the press so no ancestor with a down handler can take the click (see KitHover).
        public void OnPointerDown(PointerEventData e) { }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            if (_open) Close();
            else Open();
        }

        private void Select(int index, bool notify)
        {
            if (index < 0 || index >= _options.Count) return;
            bool changed = index != _selected;
            _selected = index;
            _label.text = _options[index];
            if (notify && changed && OnChanged != null)
            {
                try { OnChanged(index); }
                catch (System.Exception ex) { Log.Write("KitDropdown.OnChanged threw: " + ex); }
            }
        }

        public void Open()
        {
            if (_open || _options.Count == 0) return;
            KitPopup.Open(_closeHandler);
            _open = true;
            RefreshField();
            Tween.Rotate(_chevron.transform, 180f, 0.3f, Ease.OutBack);

            RectTransform layer = Kit.PopupLayer;
            RectTransform field = (RectTransform)transform;

            // Full-screen invisible blocker: pressing anywhere outside closes the list.
            _blocker = KitPopup.CreateBlocker(Close);

            float width = field.rect.width;
            bool search = SearchThreshold > 0 && _options.Count > SearchThreshold;
            float searchH = search ? SearchH + 6f : 0f;
            float listH = Mathf.Min(_options.Count * (ItemH + 2f) + 10f, MaxListH) + searchH;

            Vector3[] corners = new Vector3[4];
            field.GetWorldCorners(corners);
            Vector2 bottomLeft = Kit.WorldToLocal(layer, corners[0]);
            Vector2 topLeft = Kit.WorldToLocal(layer, corners[1]);
            float roomBelow = bottomLeft.y - layer.rect.yMin;
            bool up = roomBelow < listH + 12f && (layer.rect.yMax - topLeft.y) > roomBelow;

            _popup = Kit.Rect(layer, "DropdownList");
            Kit.Anchor(_popup, new Vector2(0.5f, 0.5f), new Vector2(0f, up ? 0f : 1f),
                up ? topLeft + new Vector2(0f, 6f) : bottomLeft - new Vector2(0f, 6f), new Vector2(width, listH));
            CanvasGroup group = _popup.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            int blur = Mathf.Max(1, Theme.ShadowSize / 2);
            Image shadow = Kit.Image(_popup, "Shadow", Sprites.Shadow(Theme.RadiusSmall, blur), new Color(0f, 0f, 0f, Theme.ShadowAlpha));
            Kit.Fill(shadow.rectTransform, -blur, -blur - 3f, -blur, -blur + 3f);
            Image bg = Kit.Image(_popup, "Bg", Sprites.Rounded(Theme.RadiusSmall), Kit.Alpha(Theme.Surface, 1f), true);
            Kit.Fill(bg.rectTransform);

            RectTransform listArea = Kit.Rect(_popup, "ListArea");
            Kit.Fill(listArea, 0f, 0f, 0f, searchH);
            KitScroll scroll = KitScroll.Create(listArea, 5, 5, 2f, _options.Count * (ItemH + 2f) + 10f > MaxListH);
            _listScroll = scroll;
            _popupGroup = group;
            _visible.Clear();
            for (int i = 0; i < _options.Count; i++) _visible.Add(i);
            for (int i = 0; i < _visible.Count; i++)
            {
                BuildItem(scroll.Content, _visible[i], group, i < 10 ? i : -1);
            }

            KitInput searchInput = null;
            if (search)
            {
                RectTransform sr = Kit.Rect(_popup, "Search");
                sr.anchorMin = new Vector2(0f, 1f);
                sr.anchorMax = new Vector2(1f, 1f);
                sr.pivot = new Vector2(0.5f, 1f);
                sr.sizeDelta = new Vector2(-10f, SearchH);
                sr.anchoredPosition = new Vector2(0f, -5f);
                searchInput = KitInput.Create(sr, "Search " + _options.Count + " options…", "", Filter);
                Kit.Fill((RectTransform)searchInput.transform);
                searchInput.OnSubmit = delegate(string q)
                {
                    if (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter)) return;
                    if (_visible.Count == 0) return;
                    Select(_visible[0], true);
                    Close();
                };
            }

            Image outline = Kit.Image(_popup, "Outline", Sprites.Outline(Theme.RadiusSmall, 1), Theme.Border);
            Kit.Fill(outline.rectTransform);

            // Keep the selected row in view, centered when possible, never scrolled past either end
            // (an unclamped offset left an empty gap below the last options).
            float viewH = listH - searchH;
            float contentH = _options.Count * (ItemH + 2f) - 2f + 10f;
            float selY = 5f + _selected * (ItemH + 2f) + ItemH * 0.5f - viewH * 0.5f;
            scroll.Content.anchoredPosition = new Vector2(0f, Mathf.Clamp(selY, 0f, Mathf.Max(0f, contentH - viewH)));

            Tween.Fade(group, 1f, 0.14f, Ease.OutQuad);
            Tween.SlideIn(_popup, new Vector2(0f, 8f), 0.18f, Ease.OutCubic);

            if (searchInput != null)
            {
                EventSystem es = EventSystem.current;
                if (es != null) es.SetSelectedGameObject(searchInput.gameObject);
                searchInput.Field.ActivateInputField();
            }
        }

        // Rebuilds the popup's rows for options containing the query (case-insensitive).
        private void Filter(string query)
        {
            if (_listScroll == null) return;
            string q = (query ?? "").Trim();
            _visible.Clear();
            for (int i = 0; i < _options.Count; i++)
            {
                if (q.Length == 0 || _options[i].IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0) _visible.Add(i);
            }
            RectTransform content = _listScroll.Content;
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            for (int i = 0; i < _visible.Count; i++) BuildItem(content, _visible[i], _popupGroup, -1);
            _listScroll.ScrollToTop();
        }

        // stagger >= 0 animates the row in with that position's delay; -1 = no entrance animation.
        private void BuildItem(RectTransform parent, int index, CanvasGroup popupGroup, int stagger)
        {
            RectTransform row = Kit.Rect(parent, "Item " + index);
            Kit.Layout(row.gameObject, -1f, ItemH);
            Image hover = Kit.Image(row, "Hover", Sprites.Rounded(Mathf.Max(0, Theme.RadiusSmall - 2)), Kit.Alpha(Theme.SurfaceHover, 0f), true);
            Kit.Fill(hover.rectTransform);

            RectTransform inner = Kit.Rect(row, "Inner");
            Kit.Fill(inner);
            CanvasGroup ig = inner.gameObject.AddComponent<CanvasGroup>();
            ig.blocksRaycasts = false;
            bool selected = index == _selected;
            if (selected)
            {
                Image bar = Kit.Image(inner, "Selected", Sprites.Pill(4f), Theme.Accent);
                Kit.Anchor(bar.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(4f, 16f));
            }
            Text t = Kit.MakeText(inner, "Label", _options[index], Theme.FontSize, selected ? Theme.Accent : Theme.Text, TextAnchor.MiddleLeft);
            Kit.Fill(t.rectTransform, 16f, 0f, 8f, 0f);

            // Stagger the first rows in.
            if (stagger >= 0)
            {
                ig.alpha = 0f;
                inner.anchoredPosition = new Vector2(-8f, 0f);
                float delay = stagger * 0.022f;
                Tween.Fade(ig, 1f, 0.2f, Ease.OutQuad, null, delay);
                Tween.MoveX(inner, 0f, 0.26f, Ease.OutCubic, null, delay);
            }

            KitHoverClick h = KitHoverClick.On(hover.gameObject);
            h.OnEnter = delegate { Tween.Tint(hover, Theme.SurfaceHover, 0.1f, Ease.OutQuad); };
            h.OnExit = delegate { Tween.Tint(hover, Kit.Alpha(Theme.SurfaceHover, 0f), 0.18f, Ease.OutQuad); };
            h.OnClick = delegate
            {
                Select(index, true);
                Close();
            };
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            KitPopup.Forget(_closeHandler);
            RefreshField();
            Tween.Rotate(_chevron.transform, 0f, 0.25f, Ease.OutCubic);

            if (_blocker != null) Destroy(_blocker.gameObject);
            _blocker = null;
            if (_popup != null)
            {
                RectTransform popup = _popup;
                CanvasGroup g = popup.GetComponent<CanvasGroup>();
                g.blocksRaycasts = false;
                Tween.Fade(g, 0f, 0.12f, Ease.InQuad, delegate { if (popup != null) Destroy(popup.gameObject); });
            }
            _popup = null;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Close();
        }
    }
}
