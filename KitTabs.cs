using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HSUIKit
{
    // Tab bar with a spring-following underline indicator + fading/sliding page switch.
    public class KitTabs : KitElement
    {
        private const float BarH = 42f;

        public RectTransform[] Pages;
        public IntHandler OnChanged;

        private RectTransform _bar;
        private RectTransform _indicator;
        private Image _indicatorImg, _divider;
        private readonly List<RectTransform> _tabs = new List<RectTransform>();
        private readonly List<Text> _labels = new List<Text>();
        private readonly List<Image> _hovers = new List<Image>();
        private int _active = -1;
        private float _indX, _indW;
        private bool _indPlaced;

        public int Active { get { return _active; } }

        public static KitTabs Create(Transform parent, string[] names)
        {
            RectTransform rt = Kit.Rect(parent, "Tabs");
            Kit.Fill(rt);
            KitTabs k = rt.gameObject.AddComponent<KitTabs>();

            k._bar = Kit.Rect(rt, "Bar");
            k._bar.anchorMin = new Vector2(0f, 1f);
            k._bar.anchorMax = new Vector2(1f, 1f);
            k._bar.pivot = new Vector2(0.5f, 1f);
            k._bar.sizeDelta = new Vector2(0f, BarH);
            k._bar.anchoredPosition = Vector2.zero;
            HorizontalLayoutGroup h = k._bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(10, 10, 0, 0);
            h.spacing = 2f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;

            k._divider = Kit.Image(rt, "Divider", null, Color.clear);
            RectTransform d = k._divider.rectTransform;
            d.anchorMin = new Vector2(0f, 1f);
            d.anchorMax = new Vector2(1f, 1f);
            d.pivot = new Vector2(0.5f, 1f);
            d.sizeDelta = new Vector2(0f, 1f);
            d.anchoredPosition = new Vector2(0f, -BarH);

            RectTransform pagesRoot = Kit.Rect(rt, "Pages");
            Kit.Fill(pagesRoot, 0f, 0f, 0f, BarH + 1f);
            k.Pages = new RectTransform[names.Length];

            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                RectTransform tab = Kit.Rect(k._bar, "Tab " + names[i]);
                HorizontalLayoutGroup th = tab.gameObject.AddComponent<HorizontalLayoutGroup>();
                th.padding = new RectOffset(14, 14, 0, 0);
                th.childAlignment = TextAnchor.MiddleCenter;
                th.childForceExpandWidth = false;
                th.childForceExpandHeight = false;
                Kit.Layout(tab.gameObject, -1f, BarH);

                Image hover = Kit.Image(tab, "Hover", null, Color.clear, true);
                Kit.Fill(hover.rectTransform, 0f, 7f, 0f, 7f);
                Kit.IgnoreLayout(hover.gameObject);
                Text label = Kit.MakeText(tab, "Label", names[i], Theme.FontSize, Theme.TextDim, TextAnchor.MiddleCenter);

                KitHoverClick hc = KitHoverClick.On(hover.gameObject);
                hc.OnEnter = delegate { if (index != k._active) { Tween.Tint(hover, k.HoverBg(), 0.12f, Ease.OutQuad); Tween.Tint(label, Theme.Text, 0.12f, Ease.OutQuad); } };
                hc.OnExit = delegate { if (index != k._active) { Tween.Tint(hover, k.TabBg(index), 0.2f, Ease.OutQuad); Tween.Tint(label, k.LabelColor(index), 0.2f, Ease.OutQuad); } };
                hc.OnClick = delegate { k.Select(index, true); };

                k._tabs.Add(tab);
                k._labels.Add(label);
                k._hovers.Add(hover);

                RectTransform page = Kit.Rect(pagesRoot, "Page " + names[i]);
                Kit.Fill(page);
                page.gameObject.AddComponent<CanvasGroup>();
                page.gameObject.SetActive(false);
                k.Pages[i] = page;
            }

            k._indicatorImg = Kit.Image(k._bar, "Indicator", null, Color.clear);
            k._indicator = k._indicatorImg.rectTransform;
            Kit.IgnoreLayout(k._indicator.gameObject);
            k._indicator.anchorMin = Vector2.zero;
            k._indicator.anchorMax = Vector2.zero;
            k._indicator.pivot = Vector2.zero;
            k._indicator.sizeDelta = new Vector2(0f, 3f);

            k.Finish();
            if (names.Length > 0) k.Select(0, false);
            return k;
        }

        // Underline style (Glass Dark): transparent tabs, hover wash, sliding accent underline.
        // Pill style (Candy): every tab is a pill, the active one filled with the accent and white text.
        internal Color TabBg(int i)
        {
            if (!Theme.PillTabs) return Kit.Alpha(Theme.SurfaceHover, 0f);
            return i == _active ? Theme.Accent : Theme.Card;
        }

        internal Color HoverBg() { return Theme.PillTabs ? Theme.SurfaceHover : Kit.Alpha(Theme.SurfaceHover, 0.8f); }

        internal Color LabelColor(int i)
        {
            if (i == _active) return Theme.PillTabs ? Color.white : Theme.Text;
            return Theme.TextDim;
        }

        protected override void ApplyTheme()
        {
            Kit.SetSprite(_indicatorImg, Sprites.Pill(3f));
            _indicatorImg.color = Theme.Accent;
            _indicatorImg.enabled = !Theme.PillTabs;
            _divider.color = Theme.Border;
            _divider.enabled = !Theme.PillTabs;
            _bar.GetComponent<HorizontalLayoutGroup>().spacing = Theme.PillTabs ? 6f : 2f;   // pills need air between them
            for (int i = 0; i < _labels.Count; i++)
            {
                _labels[i].font = Kit.Font;
                _labels[i].fontSize = Theme.FontSize;
                _labels[i].fontStyle = i == _active ? FontStyle.Bold : Theme.LabelStyle;
                _labels[i].color = LabelColor(i);
                Kit.SetSprite(_hovers[i], Sprites.Rounded(Theme.PillTabs ? 14 : Theme.RadiusSmall));
                _hovers[i].color = TabBg(i);
            }
        }

        public void Select(int index, bool animate)
        {
            if (index == _active || index < 0 || index >= Pages.Length) return;
            int prev = _active;
            _active = index;
            KitPopup.CloseAll();

            for (int i = 0; i < _labels.Count; i++)
            {
                Color c = LabelColor(i);
                if (animate) { Tween.Tint(_labels[i], c, 0.18f, Ease.OutQuad); Tween.Tint(_hovers[i], TabBg(i), 0.18f, Ease.OutQuad); }
                else { _labels[i].color = c; _hovers[i].color = TabBg(i); }
                _labels[i].fontStyle = i == index ? FontStyle.Bold : Theme.LabelStyle;
            }

            if (prev >= 0) Pages[prev].gameObject.SetActive(false);
            RectTransform page = Pages[index];
            page.gameObject.SetActive(true);
            if (animate)
            {
                CanvasGroup g = page.GetComponent<CanvasGroup>();
                g.alpha = 0f;
                float dir = index > prev ? 1f : -1f;
                page.anchoredPosition = new Vector2(18f * dir, 0f);
                Tween.Fade(g, 1f, 0.22f, Ease.OutQuad);
                Tween.Move(page, Vector2.zero, 0.3f, Ease.OutCubic);
            }
            else
            {
                page.GetComponent<CanvasGroup>().alpha = 1f;
                page.anchoredPosition = Vector2.zero;
                _indPlaced = false;
            }

            if (OnChanged != null)
            {
                try { OnChanged(index); }
                catch (System.Exception ex) { Log.Write("KitTabs.OnChanged threw: " + ex); }
            }
        }

        // Layout only resolves after LateUpdate on the first frames, so the indicator follows
        // the active tab's real rect every frame with exponential smoothing (a spring-ish
        // glide on switch, a snap the first time a real width appears).
        private void LateUpdate()
        {
            if (_active < 0) return;
            RectTransform tab = _tabs[_active];
            float w = tab.rect.width;
            if (w <= 0f) return;
            float x = tab.localPosition.x + tab.rect.xMin - _bar.rect.xMin;
            const float inset = 12f;
            float tx = x + inset, tw = Mathf.Max(8f, w - inset * 2f);
            if (!_indPlaced)
            {
                _indX = tx; _indW = tw; _indPlaced = true;
            }
            else
            {
                float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f * Mathf.Max(0.05f, Theme.AnimSpeed));
                _indX = Mathf.Lerp(_indX, tx, k);
                _indW = Mathf.Lerp(_indW, tw, k);
            }
            // Only write when it moved: RectTransform setters dirty the canvas even for equal values.
            Vector2 p = new Vector2(Mathf.Round(_indX), 0f), s = new Vector2(Mathf.Round(_indW), 3f);   // whole pixels (crisp)
            if ((_indicator.anchoredPosition - p).sqrMagnitude > 0.0001f) _indicator.anchoredPosition = p;
            if ((_indicator.sizeDelta - s).sqrMagnitude > 0.0001f) _indicator.sizeDelta = s;
        }
    }
}
