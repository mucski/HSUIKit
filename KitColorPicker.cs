using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HSUIKit
{
    // Row control: label, hex readout, color chip. Click opens the KitColorPicker popup.
    public class KitColorField : KitElement, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
    {
        private const float ChipW = 64f, ChipH = 26f;

        public ColorHandler OnChanged;   // live, while dragging in the picker
        public ColorHandler OnCommitted; // once, when the picker closes
        public bool ShowAlpha;

        private Color _value;
        private Text _label, _hex;
        private RectTransform _chip;
        private Image _checker, _color, _outline;

        public Color Value
        {
            get { return _value; }
            set { SetValue(value, false); }
        }

        public static KitColorField Create(Transform parent, string label, Color value, bool showAlpha, ColorHandler onChanged)
        {
            RectTransform rt = KitLayout.Row(parent, 10f);
            rt.name = "ColorField " + label;
            Kit.HitArea(rt.gameObject);
            Kit.Layout(rt.gameObject, -1f, 32f);
            KitColorField f = rt.gameObject.AddComponent<KitColorField>();
            f.OnChanged = onChanged;
            f.ShowAlpha = showAlpha;

            f._label = Kit.MakeText(rt, "Label", label, Theme.FontSize, Theme.Text, TextAnchor.MiddleLeft);
            f._label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            f._hex = Kit.MakeText(rt, "Hex", "", Theme.SmallSize, Theme.TextDim, TextAnchor.MiddleRight);

            f._chip = Kit.Rect(rt, "Chip");
            Kit.Layout(f._chip.gameObject, ChipW, ChipH);
            f._checker = Kit.Image(f._chip, "Checker", Sprites.Checker((int)ChipW, (int)ChipH, 6), Color.white);
            Kit.Fill(f._checker.rectTransform);
            f._color = Kit.Image(f._chip, "Color", null, Color.white);
            Kit.Fill(f._color.rectTransform);
            f._outline = Kit.Image(f._chip, "Outline", null, Color.clear);
            Kit.Fill(f._outline.rectTransform);

            f.Finish();
            f.SetValue(value, false);
            return f;
        }

        protected override void ApplyTheme()
        {
            _label.font = Kit.Font; _label.fontSize = Theme.FontSize; _label.color = Theme.Text;
            _hex.font = Kit.Font; _hex.fontSize = Theme.SmallSize; _hex.color = Theme.TextDim;
            Kit.SetSprite(_color, Sprites.Rounded(6));
            Kit.SetSprite(_outline, Sprites.Outline(6, 1));
            _outline.color = Kit.Alpha(Color.white, 0.18f);
        }

        public void SetValue(Color c, bool notify)
        {
            _value = c;
            if (!ShowAlpha) _value.a = 1f;
            _color.color = _value;
            _hex.text = ShowAlpha ? Theme.ToHex(_value) : Theme.ToHex(_value).Substring(0, 7);
            if (notify && OnChanged != null)
            {
                try { OnChanged(_value); }
                catch (System.Exception ex) { Log.Write("KitColorField.OnChanged threw: " + ex); }
            }
        }

        public void OnPointerEnter(PointerEventData e) { Tween.Scale(_chip, 1.06f, 0.2f, Ease.OutCubic); }

        public void OnPointerExit(PointerEventData e) { Tween.Scale(_chip, 1f, 0.2f, Ease.OutCubic); }

        public void OnPointerDown(PointerEventData e) { }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            KitColorPicker.Open(_chip, _value, ShowAlpha,
                delegate(Color c) { SetValue(c, true); },
                delegate(Color c)
                {
                    if (OnCommitted == null) return;
                    try { OnCommitted(c); }
                    catch (System.Exception ex) { Log.Write("KitColorField.OnCommitted threw: " + ex); }
                });
        }
    }

    // HSV picker popup: saturation/value square, hue bar, optional alpha bar, old/new preview,
    // hex entry, preset + recently-used swatches. Every drag reports live via onChanged.
    public class KitColorPicker : MonoBehaviour
    {
        private const int W = 260, Pad = 12, Inner = W - 2 * Pad, SvH = 150, BarH = 12, R = 6;

        private static readonly List<Color> Recent = new List<Color>();
        private static readonly string[] Presets =
            { "#FFFFFF", "#9AA0AE", "#1B1D22", "#F0616D", "#FF9A3D", "#F5C84E", "#3DD68C", "#5B8CFF" };

        private float _h, _s, _v, _a;
        private Color _initial;
        private bool _alpha;
        private ColorHandler _onChanged, _onClosed;
        private VoidHandler _closeHandler;
        private RectTransform _blocker;
        private bool _closing;

        private Image _svBase, _alphaFade, _newChip;
        private RectTransform _svHandle, _hueHandle, _alphaHandle;
        private KitInput _hex;

        public Color Current
        {
            get
            {
                Color c = Sprites.HsvToRgb(_h, _s, _v);
                c.a = _alpha ? _a : 1f;
                return c;
            }
        }

        public static KitColorPicker Open(RectTransform anchor, Color initial, bool alpha, ColorHandler onChanged, ColorHandler onClosed)
        {
            Kit.Ensure();
            KitTooltip.HideNow();

            RectTransform rt = Kit.Rect(Kit.PopupLayer, "ColorPicker");
            KitColorPicker p = rt.gameObject.AddComponent<KitColorPicker>();
            p._initial = initial;
            p._alpha = alpha;
            p._onChanged = onChanged;
            p._onClosed = onClosed;
            p._closeHandler = p.Close;
            Sprites.RgbToHsv(initial, out p._h, out p._s, out p._v);
            p._a = initial.a;

            KitPopup.Open(p._closeHandler);
            // Blocker must sit UNDER the picker: create it, then move it just before us.
            p._blocker = KitPopup.CreateBlocker(p.Close);
            p._blocker.SetSiblingIndex(rt.GetSiblingIndex());

            float y = Pad;
            RectTransform sv = p.BuildSv(rt, y);
            y += SvH + 10;
            p.BuildHue(rt, y);
            y += BarH + 10;
            if (alpha)
            {
                p.BuildAlpha(rt, y);
                y += BarH + 10;
            }
            y += 2;
            p.BuildPreviewRow(rt, y);
            y += 30 + 12;
            p.BuildSwatchRow(rt, y, "Presets", PresetColors());
            y += 22;
            if (Recent.Count > 0)
            {
                y += 8;
                p.BuildSwatchRow(rt, y, "Recent", Recent.ToArray());
                y += 22;
            }
            y += Pad;

            // Background layers go first (behind everything built above).
            int blur = Mathf.Max(1, Theme.ShadowSize / 2);
            Image outline = Kit.Image(rt, "Outline", Sprites.Outline(Theme.Radius, 1), Theme.Border);
            Kit.Fill(outline.rectTransform);
            Image bg = Kit.Image(rt, "Bg", Sprites.Rounded(Theme.Radius), Kit.Alpha(Theme.Surface, 1f), true);
            Kit.Fill(bg.rectTransform);
            bg.transform.SetAsFirstSibling();
            Image shadow = Kit.Image(rt, "Shadow", Sprites.Shadow(Theme.Radius, blur), new Color(0f, 0f, 0f, Theme.ShadowAlpha));
            Kit.Fill(shadow.rectTransform, -blur, -blur - 4f, -blur, -blur + 4f);
            shadow.transform.SetAsFirstSibling();

            bool up = KitPopup.PlaceNear(rt, anchor, new Vector2(W, y), 8f);
            // Anchor to the chip's right edge rather than its left when that fits better.
            Vector3[] c = new Vector3[4];
            anchor.GetWorldCorners(c);
            float right = Kit.WorldToLocal(Kit.PopupLayer, c[3]).x;
            Vector2 pos = rt.anchoredPosition;
            pos.x = Mathf.Clamp(right - W, Kit.PopupLayer.rect.xMin + 6f, Kit.PopupLayer.rect.xMax - W - 6f);
            rt.anchoredPosition = pos;

            p.Refresh(true);

            CanvasGroup g = rt.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0f;
            rt.pivot = new Vector2(1f, up ? 0f : 1f);
            rt.anchoredPosition = pos + new Vector2(W, 0f);
            Tween.Fade(g, 1f, 0.16f, Ease.OutQuad);
            Tween.SlideIn(rt, new Vector2(0f, up ? -8f : 8f), 0.2f, Ease.OutCubic);
            return p;
        }

        private static Color[] PresetColors()
        {
            Color[] c = new Color[Presets.Length];
            for (int i = 0; i < c.Length; i++) c[i] = Theme.Hex(Presets[i]);
            return c;
        }

        private static RectTransform Place(Transform parent, string name, float x, float y, float w, float h)
        {
            RectTransform rt = Kit.Rect(parent, name);
            Kit.Anchor(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, h));
            return rt;
        }

        private RectTransform BuildSv(RectTransform root, float y)
        {
            RectTransform sv = Place(root, "SV", Pad, y, Inner, SvH);
            _svBase = Kit.Image(sv, "Hue", Sprites.Rounded(R), Color.red);
            Kit.Fill(_svBase.rectTransform);
            Image white = Kit.Image(sv, "White", Sprites.FadeRight(Inner, SvH, R, true), Color.white);
            Kit.Fill(white.rectTransform);
            Image black = Kit.Image(sv, "Black", Sprites.FadeUp(Inner, SvH, R), Color.black);
            Kit.Fill(black.rectTransform);
            Image ol = Kit.Image(sv, "Outline", Sprites.Outline(R, 1), Kit.Alpha(Color.white, 0.12f));
            Kit.Fill(ol.rectTransform);

            Kit.HitArea(sv.gameObject);
            KitDragArea d = sv.gameObject.AddComponent<KitDragArea>();
            d.OnPoint = delegate(float x, float yy) { _s = x; _v = yy; Changed(); };
            d.OnRelease = delegate { Tween.Scale(_svHandle, 1f, 0.2f, Ease.OutBack); };
            KitHoverClick.On(sv.gameObject).OnDown = delegate { Tween.Scale(_svHandle, 1.25f, 0.15f, Ease.OutQuad); };

            _svHandle = Handle(sv, 16f, 16f, Sprites.Ring);
            return sv;
        }

        private void BuildHue(RectTransform root, float y)
        {
            RectTransform bar = Place(root, "HueBar", Pad, y, Inner, BarH);
            Image img = Kit.Image(bar, "Hue", Sprites.HueBar(Inner, BarH, R), Color.white);
            Kit.Fill(img.rectTransform);
            Kit.HitArea(bar.gameObject);
            KitDragArea d = bar.gameObject.AddComponent<KitDragArea>();
            d.OnPoint = delegate(float x, float yy) { _h = Mathf.Min(x, 0.9999f); Changed(); };
            _hueHandle = Handle(bar, 8f, 20f, Sprites.Pill(8f));
        }

        private void BuildAlpha(RectTransform root, float y)
        {
            RectTransform bar = Place(root, "AlphaBar", Pad, y, Inner, BarH);
            Image checker = Kit.Image(bar, "Checker", Sprites.Checker(Inner, BarH, R), Color.white);
            Kit.Fill(checker.rectTransform);
            _alphaFade = Kit.Image(bar, "Fade", Sprites.FadeRight(Inner, BarH, R, false), Color.white);
            Kit.Fill(_alphaFade.rectTransform);
            Kit.HitArea(bar.gameObject);
            KitDragArea d = bar.gameObject.AddComponent<KitDragArea>();
            d.OnPoint = delegate(float x, float yy) { _a = x; Changed(); };
            _alphaHandle = Handle(bar, 8f, 20f, Sprites.Pill(8f));
        }

        // White handle with a soft dark halo so it reads on any color.
        private static RectTransform Handle(RectTransform parent, float w, float h, Sprite sprite)
        {
            RectTransform holder = Kit.Rect(parent, "Handle");
            Kit.Anchor(holder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
            Image shadow = Kit.Image(holder, "Shadow", Sprites.CircleShadow, new Color(0f, 0f, 0f, 0.45f));
            float s = Mathf.Max(w, h) * 64f / 40f;
            Kit.Anchor(shadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(s * (w / Mathf.Max(w, h)) + 8f, s * (h / Mathf.Max(w, h))));
            Image img = Kit.Image(holder, "Knob", sprite, Color.white);
            Kit.Fill(img.rectTransform);
            return holder;
        }

        private void BuildPreviewRow(RectTransform root, float y)
        {
            RectTransform oldChip = Place(root, "Old", Pad, y, 26f, 30f);
            Chip(oldChip, _initial);
            KitTooltip.Attach(oldChip.gameObject, "Original - click to revert");
            KitHoverClick h = KitHoverClick.On(oldChip.gameObject);
            h.OnClick = delegate
            {
                Sprites.RgbToHsv(_initial, out _h, out _s, out _v);
                _a = _initial.a;
                Changed();
            };
            RectTransform newChip = Place(root, "New", Pad + 28f, y, 26f, 30f);
            _newChip = Chip(newChip, Current);

            RectTransform hexRt = Place(root, "HexHolder", Pad + 64f, y, Inner - 64f, 30f);
            _hex = KitInput.Create(hexRt, _alpha ? "#RRGGBBAA" : "#RRGGBB", "", null);
            Kit.Fill((RectTransform)_hex.transform);
            _hex.Field.characterLimit = 9;
            _hex.OnSubmit = delegate(string s)
            {
                Color c;
                if (TryParseHex(s, out c))
                {
                    Sprites.RgbToHsv(c, out _h, out _s, out _v);
                    _a = c.a;
                    Changed();
                }
                Refresh(true);
            };
        }

        private static Image Chip(RectTransform rt, Color c)
        {
            Kit.HitArea(rt.gameObject);
            Image checker = Kit.Image(rt, "Checker", Sprites.Checker(26, 30, 5), Color.white);
            Kit.Fill(checker.rectTransform);
            Image color = Kit.Image(rt, "Color", Sprites.Rounded(5), c);
            Kit.Fill(color.rectTransform);
            Image ol = Kit.Image(rt, "Outline", Sprites.Outline(5, 1), Kit.Alpha(Color.white, 0.15f));
            Kit.Fill(ol.rectTransform);
            return color;
        }

        private void BuildSwatchRow(RectTransform root, float y, string name, Color[] colors)
        {
            RectTransform row = Place(root, name, Pad, y, Inner, 22f);
            HorizontalLayoutGroup hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = (Inner - 8 * 22f) / 7f;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            for (int i = 0; i < colors.Length && i < 8; i++)
            {
                Color c = colors[i];
                KitSwatch.Create(row, c, 22f, delegate
                {
                    Sprites.RgbToHsv(c, out _h, out _s, out _v);
                    _a = c.a;
                    Changed();
                });
            }
        }

        private void Changed()
        {
            Refresh(false);
            if (_onChanged != null)
            {
                try { _onChanged(Current); }
                catch (System.Exception ex) { Log.Write("KitColorPicker onChanged threw: " + ex); }
            }
        }

        private void Refresh(bool forceHex)
        {
            Color c = Current;
            _svBase.color = Sprites.HsvToRgb(_h, 1f, 1f);
            SetAnchor(_svHandle, _s, _v);
            SetAnchor(_hueHandle, _h, 0.5f);
            if (_alphaHandle != null)
            {
                SetAnchor(_alphaHandle, _a, 0.5f);
                _alphaFade.color = new Color(c.r, c.g, c.b, 1f);
            }
            _newChip.color = c;
            if (forceHex || !_hex.Field.isFocused)
            {
                string hex = Theme.ToHex(c);
                _hex.Text = _alpha ? hex : hex.Substring(0, 7);
            }
        }

        private static void SetAnchor(RectTransform rt, float x, float y)
        {
            Vector2 a = new Vector2(x, y);
            rt.anchorMin = a;
            rt.anchorMax = a;
            rt.anchoredPosition = Vector2.zero;
        }

        private static bool TryParseHex(string s, out Color c)
        {
            c = Color.white;
            s = (s ?? "").Trim().TrimStart('#');
            if (s.Length != 6 && s.Length != 8) return false;
            for (int i = 0; i < s.Length; i++)
            {
                if (System.Uri.IsHexDigit(s[i])) continue;
                return false;
            }
            c = Theme.Hex("#" + s);
            return true;
        }

        public void Close()
        {
            if (_closing) return;
            _closing = true;
            KitPopup.Forget(_closeHandler);
            if (_blocker != null) Destroy(_blocker.gameObject);

            Color result = Current;
            if (Theme.ToHex(result) != Theme.ToHex(_initial))
            {
                for (int i = Recent.Count - 1; i >= 0; i--)
                {
                    if (Theme.ToHex(Recent[i]) == Theme.ToHex(result)) Recent.RemoveAt(i);
                }
                Recent.Insert(0, result);
                if (Recent.Count > 8) Recent.RemoveAt(Recent.Count - 1);
            }

            CanvasGroup g = GetComponent<CanvasGroup>();
            g.blocksRaycasts = false;
            GameObject go = gameObject;
            Tween.Fade(g, 0f, 0.12f, Ease.InQuad, delegate { if (go != null) Destroy(go); });

            if (_onClosed != null)
            {
                try { _onClosed(result); }
                catch (System.Exception ex) { Log.Write("KitColorPicker onClosed threw: " + ex); }
            }
        }
    }
}
