using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HSUIKit
{
    // Searchable, selectable list that stays fast with thousands of rows: only the rows
    // visible in the viewport exist (a small pool, re-bound as you scroll). Matches of the
    // search text are highlighted. OnSelected/OnActivated report ORIGINAL item indices.
    public class KitListView : KitElement
    {
        private const float RowH = 30f, Pad = 4f;

        public IntHandler OnSelected;
        public IntHandler OnActivated; // double-click
        public MenuBuilder ContextMenu; // optional right-click menu (row is selected first)

        private readonly List<string> _items = new List<string>();
        private readonly List<string> _details = new List<string>();
        private readonly List<int> _filtered = new List<int>();
        private readonly List<KitListRow> _pool = new List<KitListRow>();
        private string _query = "";
        private int _selected = -1;

        private KitInput _search;
        private Text _count, _empty;
        private Image _bg, _outline;
        private KitScroll _scroll;
        private float _lastY = -1f, _lastViewH = -1f;
        private bool _dirty = true;

        public int Selected
        {
            get { return _selected; }
            set { _selected = value; _dirty = true; }
        }

        public string SelectedText
        {
            get { return _selected >= 0 && _selected < _items.Count ? _items[_selected] : null; }
        }

        public int Count { get { return _items.Count; } }

        public static KitListView Create(Transform parent, float height, bool searchable)
        {
            RectTransform rt = Kit.Rect(parent, "ListView");
            Kit.Layout(rt.gameObject, -1f, height);
            KitListView l = rt.gameObject.AddComponent<KitListView>();

            float top = 0f;
            if (searchable)
            {
                RectTransform sr = Kit.Rect(rt, "SearchRow");
                sr.anchorMin = new Vector2(0f, 1f);
                sr.anchorMax = new Vector2(1f, 1f);
                sr.pivot = new Vector2(0.5f, 1f);
                sr.sizeDelta = new Vector2(0f, 34f);
                sr.anchoredPosition = Vector2.zero;
                l._search = KitInput.Create(sr, "Search…", "", delegate(string s) { l.SetQuery(s); });
                Kit.Fill((RectTransform)l._search.transform, 0f, 0f, 84f, 0f);
                l._count = Kit.MakeText(sr, "Count", "", Theme.SmallSize, Theme.TextDim, TextAnchor.MiddleRight);
                l._count.rectTransform.anchorMin = new Vector2(1f, 0f);
                l._count.rectTransform.anchorMax = new Vector2(1f, 1f);
                l._count.rectTransform.pivot = new Vector2(1f, 0.5f);
                l._count.rectTransform.sizeDelta = new Vector2(78f, 0f);
                l._count.rectTransform.anchoredPosition = Vector2.zero;
                top = 42f;
            }

            RectTransform box = Kit.Rect(rt, "Box");
            Kit.Fill(box, 0f, 0f, 0f, top);
            l._bg = box.gameObject.AddComponent<Image>();
            l._bg.raycastTarget = false;
            l._scroll = KitScroll.Create(box, 0, 0, 0f, true, false);
            l._outline = Kit.Image(box, "Outline", null, Color.clear);
            Kit.Fill(l._outline.rectTransform);
            l._empty = Kit.MakeText(box, "Empty", "No matches", Theme.FontSize, Theme.TextDim, TextAnchor.MiddleCenter);
            Kit.Fill(l._empty.rectTransform);
            l._empty.fontStyle = FontStyle.Italic;

            l.Finish();
            return l;
        }

        protected override void ApplyTheme()
        {
            Kit.SetSprite(_bg, Sprites.Rounded(Theme.RadiusSmall));
            _bg.color = Kit.Shade(Theme.Surface, -0.12f);
            Kit.SetSprite(_outline, Sprites.Outline(Theme.RadiusSmall, 1));
            _outline.color = Theme.Border;
            _empty.font = Kit.Font;
            _empty.color = Theme.TextDim;
            if (_count != null) { _count.font = Kit.Font; _count.fontSize = Theme.SmallSize; _count.color = Theme.TextDim; }
            for (int i = 0; i < _pool.Count; i++) _pool[i].ApplyTheme();
            _dirty = true;
        }

        public void SetItems(IList<string> items, IList<string> details = null)
        {
            _items.Clear();
            _details.Clear();
            _items.AddRange(items);
            for (int i = 0; i < _items.Count; i++)
            {
                _details.Add(details != null && i < details.Count ? details[i] : null);
            }
            if (_selected >= _items.Count) _selected = -1;
            Refilter();
        }

        public void SetQuery(string q)
        {
            _query = (q ?? "").Trim();
            Refilter();
            _scroll.ScrollToTop();
        }

        private void Refilter()
        {
            _filtered.Clear();
            for (int i = 0; i < _items.Count; i++)
            {
                if (_query.Length == 0 || _items[i].IndexOf(_query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (_details[i] != null && _details[i].IndexOf(_query, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    _filtered.Add(i);
                }
            }
            _scroll.Content.sizeDelta = new Vector2(0f, _filtered.Count * RowH + Pad * 2f);
            if (_count != null) _count.text = _query.Length == 0 ? _items.Count + " items" : _filtered.Count + " / " + _items.Count;
            _empty.enabled = _filtered.Count == 0;
            _empty.text = _items.Count == 0 ? "Empty" : "No matches";
            _dirty = true;
        }

        private void LateUpdate()
        {
            float y = _scroll.Content.anchoredPosition.y;
            float vh = _scroll.ViewportHeight;
            if (!_dirty && Mathf.Approximately(y, _lastY) && Mathf.Approximately(vh, _lastViewH)) return;
            _dirty = false;
            _lastY = y;
            _lastViewH = vh;
            Rebind(y, vh);
        }

        private void Rebind(float scrollY, float viewH)
        {
            int first = Mathf.Max(0, Mathf.FloorToInt((scrollY - Pad) / RowH));
            int visible = Mathf.CeilToInt(viewH / RowH) + 2;
            while (_pool.Count < visible) _pool.Add(KitListRow.Create(this, _scroll.Content));

            string hex = Theme.ToHex(Theme.Accent);
            for (int j = 0; j < _pool.Count; j++)
            {
                KitListRow row = _pool[j];
                int fi = first + j;
                if (j >= visible || fi >= _filtered.Count)
                {
                    if (row.gameObject.activeSelf) row.gameObject.SetActive(false);
                    continue;
                }
                if (!row.gameObject.activeSelf) row.gameObject.SetActive(true);
                int item = _filtered[fi];
                row.Rect.anchoredPosition = new Vector2(0f, -(Pad + fi * RowH));
                row.Bind(item, Highlight(_items[item], hex), _details[item], item == _selected);
            }
        }

        private string Highlight(string text, string accentHex)
        {
            if (_query.Length == 0) return text;
            int i = text.IndexOf(_query, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return text;
            return text.Substring(0, i) + "<color=" + accentHex + "><b>" + text.Substring(i, _query.Length) + "</b></color>" +
                   text.Substring(i + _query.Length);
        }

        internal void RowClicked(int item, int clickCount)
        {
            if (item != _selected)
            {
                _selected = item;
                _dirty = true;
                if (OnSelected != null)
                {
                    try { OnSelected(item); }
                    catch (Exception ex) { Log.Write("KitListView.OnSelected threw: " + ex); }
                }
            }
            if (clickCount >= 2 && OnActivated != null)
            {
                try { OnActivated(item); }
                catch (Exception ex) { Log.Write("KitListView.OnActivated threw: " + ex); }
            }
        }

        internal void RowRightClicked(int item, Vector2 screen)
        {
            RowClicked(item, 1);
            if (ContextMenu == null) return;
            KitMenu menu = null;
            try { menu = ContextMenu(); }
            catch (Exception ex) { Log.Write("KitListView.ContextMenu threw: " + ex); }
            if (menu != null) KitContextMenu.Show(menu, screen);
        }
    }

    public class KitListRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
    {
        public RectTransform Rect;

        private KitListView _owner;
        private Image _hover, _bar;
        private Text _text, _detail;
        private int _item;
        private bool _selected, _isHover;

        internal static KitListRow Create(KitListView owner, RectTransform content)
        {
            RectTransform rt = Kit.Rect(content, "Row");
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-8f, 28f);
            KitListRow r = rt.gameObject.AddComponent<KitListRow>();
            r.Rect = rt;
            r._owner = owner;
            r._item = -1;
            r._hover = rt.gameObject.AddComponent<Image>();
            r._hover.raycastTarget = true;
            r._bar = Kit.Image(rt, "Bar", Sprites.Pill(4f), Color.clear);
            Kit.Anchor(r._bar.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(4f, 16f));
            r._text = Kit.MakeText(rt, "Text", "", Theme.FontSize, Theme.Text, TextAnchor.MiddleLeft);
            Kit.Fill(r._text.rectTransform, 16f, 0f, 90f, 0f);
            r._text.horizontalOverflow = HorizontalWrapMode.Wrap; // clip long names instead of spilling
            r._text.verticalOverflow = VerticalWrapMode.Truncate;
            r._detail = Kit.MakeText(rt, "Detail", "", Theme.SmallSize, Theme.TextDim, TextAnchor.MiddleRight);
            Kit.Fill(r._detail.rectTransform, 0f, 0f, 12f, 0f);
            r.ApplyTheme();
            return r;
        }

        internal void ApplyTheme()
        {
            Kit.SetSprite(_hover, Sprites.Rounded(Mathf.Max(0, Theme.RadiusSmall - 2)));
            _text.font = Kit.Font; _text.fontSize = Theme.FontSize;
            _detail.font = Kit.Font; _detail.fontSize = Theme.SmallSize; _detail.color = Theme.TextDim;
        }

        internal void Bind(int item, string richText, string detail, bool selected)
        {
            bool changedItem = item != _item;
            _item = item;
            _text.text = richText;
            _detail.text = detail ?? "";
            if (changedItem || selected != _selected)
            {
                _selected = selected;
                Tween.Kill(_hover, "color");
                _hover.color = BgColor();
            }
            _bar.color = selected ? Theme.Accent : Color.clear;
            _text.color = selected ? Theme.Text : Kit.Shade(Theme.Text, -0.1f);
            _text.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
        }

        private Color BgColor()
        {
            if (_selected) return Kit.Alpha(Theme.Accent, _isHover ? 0.26f : 0.18f);
            return _isHover ? Theme.SurfaceHover : Kit.Alpha(Theme.SurfaceHover, 0f);
        }

        public void OnPointerEnter(PointerEventData e) { _isHover = true; Tween.Tint(_hover, BgColor(), 0.1f, Ease.OutQuad); }

        public void OnPointerExit(PointerEventData e) { _isHover = false; Tween.Tint(_hover, BgColor(), 0.18f, Ease.OutQuad); }

        public void OnPointerDown(PointerEventData e) { }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) _owner.RowClicked(_item, e.clickCount);
            else if (e.button == PointerEventData.InputButton.Right) _owner.RowRightClicked(_item, e.position);
        }
    }
}
