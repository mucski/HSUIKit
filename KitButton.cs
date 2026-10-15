using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HSUIKit
{
    // Positive/Warm were added in 1.3.0 at the end so older plugins' compiled values keep their meaning.
    public enum ButtonStyle { Primary, Secondary, Ghost, Danger, Positive, Warm }

    // Custom button (not UnityEngine.UI.Button - its ColorTint transition would fight the
    // tweens): hover brighten, press darken, click ripple. No size change on hover/press: a scaled
    // button sits between pixels (soft label, slice seams under MSAA).
    public class KitButton : KitElement, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public RectTransform Rect;
        public VoidHandler OnClick;

        private ButtonStyle _style;
        private Image _bg;
        private Image _outline;
        private Text _label;
        private Image _icon;
        private CanvasGroup _group;
        private bool _hover;
        private bool _down;
        private bool _interactable = true;

        public string Label
        {
            get { return _label.text; }
            set { _label.text = value; _fitPending = true; }
        }

        // Light themes need more: at 0.4 a disabled button nearly vanished on Candy Light's pale panels.
        private static float DisabledAlpha { get { return Theme.PillButtons ? 0.55f : 0.4f; } }

        private bool _fitPending = true;
        private float _askedWidth = -2f;   // width the plugin gave the button (-1 = auto), captured on first fit
        private HorizontalLayoutGroup _row;

        public ButtonStyle Style
        {
            get { return _style; }
            set { _style = value; ApplyThemeNow(); }
        }

        public bool Interactable
        {
            get { return _interactable; }
            set
            {
                if (value == _interactable && _group != null && _group.blocksRaycasts == value)
                    return; // pages set this every frame; the refresh restarted a tint tween each time
                _interactable = value;
                _group.alpha = value ? 1f : DisabledAlpha;
                _group.blocksRaycasts = value;
                if (!value) { _hover = false; _down = false; }
                Refresh(true);
            }
        }

        // `icon` (optional) is drawn before the label, tinted like the text. An empty label with an
        // icon gives a square icon-only button.
        public static KitButton Create(Transform parent, string label, ButtonStyle style, VoidHandler onClick, Sprite icon = null)
        {
            bool iconOnly = icon != null && string.IsNullOrEmpty(label);
            RectTransform rt = Kit.Rect(parent, "Button " + label);
            KitButton b = rt.gameObject.AddComponent<KitButton>();
            b.Rect = rt;
            b._style = style;
            b.OnClick = onClick;
            b._group = rt.gameObject.AddComponent<CanvasGroup>();
            b._bg = rt.gameObject.AddComponent<Image>();
            b._bg.raycastTarget = true;

            HorizontalLayoutGroup h = b._row = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            // Icon-only: no padding, so MiddleCenter centers the icon at any button size (with 9+9 padding
            // a button made narrower than 34 px overflowed and the icon sat right of center).
            h.padding = iconOnly ? new RectOffset(0, 0, 0, 0) : new RectOffset(icon != null ? 14 : 18, 18, 0, 0);
            h.spacing = 8f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            LayoutElement le = Kit.Layout(rt.gameObject, -1f, 34f);
            le.minWidth = iconOnly ? 34f : 76f;

            if (icon != null)
            {
                b._icon = Kit.Image(rt, "Icon", icon, Color.white);
                Kit.Layout(b._icon.gameObject, 16f, 16f);
            }
            b._label = Kit.MakeText(rt, "Label", label ?? "", Theme.FontSize, Color.white, TextAnchor.MiddleCenter);
            if (iconOnly) b._label.gameObject.SetActive(false);

            b._outline = Kit.Image(rt, "Outline", null, Color.clear);
            Kit.Fill(b._outline.rectTransform);
            Kit.IgnoreLayout(b._outline.gameObject);

            b.Finish();
            return b;
        }

        protected override void ApplyTheme()
        {
            int r = Theme.PillButtons ? 17 : Theme.RadiusSmall;   // buttons are 34 tall: 17 = fully round
            Kit.SetSprite(_bg, Sprites.Rounded(r));
            Kit.SetSprite(_outline, Sprites.Outline(r, 1));
            _label.font = Kit.Font;
            _label.fontSize = Theme.FontSize;
            _label.fontStyle = Theme.LabelStyle;
            _fitPending = true;
            if (!_interactable) _group.alpha = DisabledAlpha;
            Color n, hov, p, txt, ol;
            Colors(out n, out hov, out p, out txt, out ol);
            _label.color = txt;
            if (_icon != null) _icon.color = txt;
            _outline.color = ol;
            Refresh(false);
        }

        private void Colors(out Color normal, out Color hover, out Color press, out Color text, out Color outline)
        {
            switch (_style)
            {
                case ButtonStyle.Primary:
                    normal = Theme.Accent; hover = Kit.Shade(Theme.Accent, 0.12f); press = Kit.Shade(Theme.Accent, -0.14f);
                    text = Color.white; outline = Color.clear; break;
                case ButtonStyle.Danger:
                    normal = Theme.Error; hover = Kit.Shade(Theme.Error, 0.12f); press = Kit.Shade(Theme.Error, -0.14f);
                    text = Color.white; outline = Color.clear; break;
                case ButtonStyle.Positive:
                    normal = Theme.Positive; hover = Kit.Shade(Theme.Positive, 0.12f); press = Kit.Shade(Theme.Positive, -0.14f);
                    text = Color.white; outline = Color.clear; break;
                case ButtonStyle.Warm:
                    normal = Theme.Warm; hover = Kit.Shade(Theme.Warm, 0.12f); press = Kit.Shade(Theme.Warm, -0.14f);
                    text = Color.white; outline = Color.clear; break;
                case ButtonStyle.Ghost:
                    normal = Kit.Alpha(Theme.SurfaceHover, 0f); hover = Kit.Alpha(Theme.SurfaceHover, 0.8f); press = Theme.SurfaceActive;
                    text = Theme.Text; outline = Color.clear; break;
                default:
                    normal = Theme.Surface; hover = Theme.SurfaceHover; press = Theme.SurfaceActive;
                    text = Theme.Text; outline = Theme.Border; break;
            }
        }

        // Plugins often give buttons a fixed width; a theme with wider (bold) labels then overflowed it
        // ("Disable" in a 44 px button). Runs after the plugin's own layout calls: first tightens the side
        // padding, then widens the button just enough. Shrinks back to the asked width when it fits again.
        private void LateUpdate()
        {
            if (!_fitPending || _label == null || !_label.gameObject.activeSelf) return;
            _fitPending = false;
            LayoutElement le = GetComponent<LayoutElement>();
            if (_askedWidth < -1f) _askedWidth = le != null ? le.preferredWidth : -1f;
            if (_askedWidth < 0f) return;   // auto-sized: the layout already fits the label
            float text = _label.preferredWidth + (_icon != null ? 24f : 0f);
            int pad = text + 36f <= _askedWidth ? 18 : 10;
            if (_row.padding.left != pad && _icon == null) _row.padding = new RectOffset(pad, pad, 0, 0);
            float need = Mathf.Ceil(text + pad * 2);
            float w = Mathf.Max(_askedWidth, need);
            if (le.preferredWidth != w) { le.preferredWidth = w; le.minWidth = w; }
        }

        private void Refresh(bool animate)
        {
            Color n, hov, p, txt, ol;
            Colors(out n, out hov, out p, out txt, out ol);
            Color target = _down ? p : (_hover ? hov : n);
            if (animate) Tween.Tint(_bg, target, 0.14f, Ease.OutQuad);
            else { Tween.Kill(_bg, "color"); _bg.color = target; }
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (!_interactable) return;
            _hover = true;
            Refresh(true);
        }

        public void OnPointerExit(PointerEventData e)
        {
            _hover = false;
            Refresh(true);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!_interactable || e.button != PointerEventData.InputButton.Left) return;
            _down = true;
            Refresh(true);
            SpawnRipple(e.position);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_down) return;
            _down = false;
            Refresh(true);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!_interactable || e.button != PointerEventData.InputButton.Left) return;
            Invoke(OnClick);
        }

        private void SpawnRipple(Vector2 screen)
        {
            bool light = _style == ButtonStyle.Primary || _style == ButtonStyle.Danger || _style == ButtonStyle.Positive || _style == ButtonStyle.Warm;
            KitRipple.Play(Rect, 0, screen, Theme.PillButtons ? 17 : Theme.RadiusSmall, true, true,
                light ? new Color(1f, 1f, 1f, 0.3f) : Kit.Alpha(Theme.Text, 0.14f));
        }
    }

    // Round icon-only button (window close, toolbar-ish actions).
    public class KitIconButton : KitElement, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public RectTransform Rect;
        public VoidHandler OnClick;

        private Image _bg;
        private Image _icon;
        private bool _danger;
        private CanvasGroup _group;
        private bool _shown = true;

        public Image Icon { get { return _icon; } }

        // Fades the button out (and makes it unclickable) while keeping its layout slot, so
        // neighbours don't jump when it appears/disappears.
        public void SetShown(bool shown, bool animate)
        {
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            if (shown == _shown && animate) return;
            _shown = shown;
            _group.blocksRaycasts = shown;
            if (animate)
            {
                Tween.Fade(_group, shown ? 1f : 0f, 0.18f, Ease.OutQuad);
                Tween.Scale(transform, shown ? 1f : 0.6f, 0.25f, shown ? Ease.OutBack : Ease.InQuad);
            }
            else
            {
                Tween.Kill(_group, "alpha");
                Tween.Kill(transform, "scale");
                _group.alpha = shown ? 1f : 0f;
                transform.localScale = shown ? Vector3.one : new Vector3(0.6f, 0.6f, 1f);
            }
        }
        private bool _hover;
        private bool _interactable = true;

        // Disabled: dimmed icon, no hover or clicks (e.g. a pager arrow on the first/last page).
        public bool Interactable
        {
            get { return _interactable; }
            set
            {
                if (_interactable == value) return;
                _interactable = value;
                _hover = false;
                Tween.Kill(_bg, "color"); Tween.Kill(_icon, "color");
                ApplyThemeNow();
            }
        }

        // Set for buttons that sit on a window's title bar: on a coloured bar (Candy themes) the icon rests
        // in Theme.TitleText and the hover disc is a light wash instead of a surface colour.
        // Restyles at once: KitWindow sets it right after Create, and the button had already styled itself
        // as a plain button (dark icon on the coloured bar until the next theme refresh).
        public bool OnTitleBar
        {
            get { return _onTitleBar; }
            set { if (_onTitleBar == value) return; _onTitleBar = value; ApplyThemeNow(); }
        }
        private bool _onTitleBar;
        private bool BarColoured { get { return OnTitleBar && Theme.TitleBar.a > 0.01f; } }
        private Color RestIcon { get { Color c = BarColoured ? Theme.TitleText : Theme.TextDim; return _interactable ? c : Kit.Alpha(c, c.a * 0.3f); } }
        private Color HoverIcon { get { return _danger ? Color.white : (BarColoured ? Theme.TitleText : Theme.Text); } }

        public static KitIconButton Create(Transform parent, Sprite icon, float size, bool danger, VoidHandler onClick)
        {
            RectTransform rt = Kit.Rect(parent, "IconButton");
            rt.sizeDelta = new Vector2(size, size);
            Kit.Layout(rt.gameObject, size, size);
            KitIconButton b = rt.gameObject.AddComponent<KitIconButton>();
            b.Rect = rt;
            b.OnClick = onClick;
            b._danger = danger;
            b._bg = Kit.Image(rt, "Bg", Sprites.Circle, Color.clear, true);
            Kit.Fill(b._bg.rectTransform);
            b._icon = Kit.Image(rt, "Icon", icon, Color.white);
            Kit.Fill(b._icon.rectTransform, size * 0.2f, size * 0.2f, size * 0.2f, size * 0.2f);
            b.Finish();
            return b;
        }

        protected override void ApplyTheme()
        {
            _bg.color = HoverBg(_hover);
            _icon.color = _hover ? HoverIcon : RestIcon;
        }

        private Color HoverBg(bool hover)
        {
            Color c = _danger ? Theme.Error : (BarColoured ? new Color(1f, 1f, 1f, 0.25f) : Theme.SurfaceActive);
            if (BarColoured && !_danger) return hover ? c : Kit.Alpha(c, 0f);
            return Kit.Alpha(c, hover ? (_danger ? 0.9f : 1f) : 0f);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (!_interactable) return;
            _hover = true;
            Tween.Tint(_bg, HoverBg(true), 0.15f, Ease.OutQuad);
            Tween.Tint(_icon, HoverIcon, 0.15f, Ease.OutQuad);
        }

        public void OnPointerExit(PointerEventData e)
        {
            _hover = false;
            Tween.Tint(_bg, HoverBg(false), 0.2f, Ease.OutQuad);
            Tween.Tint(_icon, RestIcon, 0.2f, Ease.OutQuad);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!_interactable) return;
            Tween.Tint(_bg, Kit.Shade(HoverBg(true), -0.1f), 0.08f, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData e)
        {
            Tween.Tint(_bg, HoverBg(_hover), 0.15f, Ease.OutQuad);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (_interactable && e.button == PointerEventData.InputButton.Left) Invoke(OnClick);
        }
    }
}
