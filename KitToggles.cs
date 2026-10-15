using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HSUIKit
{
    // iOS-style switch: label on the left, pill track + sliding knob on the right.
    public class KitSwitch : KitElement, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        private const float TrackW = 42f, TrackH = 24f, Knob = 18f;

        public BoolHandler OnChanged;

        private Text _label;
        private Image _track;
        private RectTransform _knob;
        private Image _knobImage;
        private RectTransform _knobShadow;
        private bool _on;
        private bool _hover;

        public bool Value
        {
            get { return _on; }
            set { SetValue(value, true, false); }
        }

        // Row height is a minimum, not fixed: a label that wraps makes the row taller.
        internal static void GrowWithLabel(GameObject row, float minHeight)
        {
            LayoutElement le = row.GetComponent<LayoutElement>();
            if (le == null) return;
            le.preferredHeight = -1f;
            le.minHeight = minHeight;
        }

        public static KitSwitch Create(Transform parent, string label, bool value, BoolHandler onChanged)
        {
            RectTransform rt = KitLayout.Row(parent, 10f);
            rt.name = "Switch " + label;
            Kit.HitArea(rt.gameObject);
            Kit.Layout(rt.gameObject, -1f, 30f);
            GrowWithLabel(rt.gameObject, 30f);
            KitSwitch s = rt.gameObject.AddComponent<KitSwitch>();
            s.OnChanged = onChanged;
            s._on = value;

            s._label = Kit.MakeText(rt, "Label", label, Theme.FontSize, Theme.Text, TextAnchor.MiddleLeft);
            s._label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            s._label.horizontalOverflow = HorizontalWrapMode.Wrap;   // long labels wrap and the row grows, not clipped
            s._label.verticalOverflow = VerticalWrapMode.Overflow;
            // No label (a switch in a table cell): drop it, or the row's 10 px gap pushes the track out of a
            // track-sized box (HSLRE's effect list: 8 px over the edge).
            if (string.IsNullOrEmpty(label)) s._label.gameObject.SetActive(false);

            s._track = Kit.Image(rt, "Track", Sprites.Pill(TrackH), Color.gray);
            Kit.Layout(s._track.gameObject, TrackW, TrackH);

            s._knobShadow = Kit.Image(s._track.rectTransform, "KnobShadow", Sprites.CircleShadow, new Color(0f, 0f, 0f, 0.35f)).rectTransform;
            float shadowSize = Knob * 64f / 40f;
            Kit.Anchor(s._knobShadow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(KnobX(value), -1f), new Vector2(shadowSize, shadowSize));

            s._knobImage = Kit.Image(s._track.rectTransform, "Knob", Sprites.Circle, Color.white);
            s._knob = s._knobImage.rectTransform;
            Kit.Anchor(s._knob, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(KnobX(value), 0f), new Vector2(Knob, Knob));

            s.Finish();
            return s;
        }

        private static float KnobX(bool on)
        {
            return (on ? 1f : -1f) * (TrackW - TrackH) * 0.5f;
        }

        private Color TrackColor(bool on)
        {
            return on ? (Theme.PillButtons ? Theme.Positive : Theme.Accent) : Theme.Track;
        }

        protected override void ApplyTheme()
        {
            _label.font = Kit.Font;
            _label.fontSize = Theme.FontSize;
            _label.color = Theme.Text;
            Tween.Kill(_track, "color");
            _track.color = TrackColor(_on);
        }

        public void SetValue(bool on, bool animate, bool notify)
        {
            if (on == _on && animate) return;
            _on = on;
            float x = KnobX(on);
            if (animate)
            {
                Tween.MoveX(_knob, x, 0.32f, Ease.OutBack);
                Tween.MoveX(_knobShadow, x, 0.32f, Ease.OutBack);
                Tween.Tint(_track, TrackColor(on), 0.2f, Ease.OutQuad);
            }
            else
            {
                _knob.anchoredPosition = new Vector2(x, 0f);
                _knobShadow.anchoredPosition = new Vector2(x, -1f);
                _track.color = TrackColor(on);
            }
            if (notify && OnChanged != null)
            {
                try { OnChanged(on); }
                catch (System.Exception ex) { Log.Write("KitSwitch.OnChanged threw: " + ex); }
            }
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _hover = true;
            Tween.Scale(_knob, 1.1f, 0.2f, Ease.OutCubic);
        }

        public void OnPointerExit(PointerEventData e)
        {
            _hover = false;
            Tween.Scale(_knob, 1f, 0.2f, Ease.OutCubic);
            Tween.Size(_knob, new Vector2(Knob, Knob), 0.2f, Ease.OutCubic);
        }

        public void OnPointerDown(PointerEventData e)
        {
            Tween.Size(_knob, new Vector2(Knob + 5f, Knob), 0.12f, Ease.OutQuad); // squish
        }

        public void OnPointerUp(PointerEventData e)
        {
            Tween.Size(_knob, new Vector2(Knob, Knob), 0.3f, Ease.OutBack);
            if (!_hover) Tween.Scale(_knob, 1f, 0.2f, Ease.OutCubic);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) SetValue(!_on, true, true);
        }
    }

    public class KitCheckbox : KitElement, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
    {
        private const float Box = 20f;

        public BoolHandler OnChanged;

        private Image _outline;
        private Image _fill;
        private Image _check;
        private Text _label;
        private bool _on;
        private bool _hover;

        public bool Value
        {
            get { return _on; }
            set { SetValue(value, true, false); }
        }

        public static KitCheckbox Create(Transform parent, string label, bool value, BoolHandler onChanged)
        {
            RectTransform rt = KitLayout.Row(parent, 10f);
            rt.name = "Checkbox " + label;
            Kit.HitArea(rt.gameObject);
            Kit.Layout(rt.gameObject, -1f, 28f);
            KitSwitch.GrowWithLabel(rt.gameObject, 28f);
            KitCheckbox c = rt.gameObject.AddComponent<KitCheckbox>();
            c.OnChanged = onChanged;
            c._on = value;

            RectTransform box = Kit.Rect(rt, "Box");
            Kit.Layout(box.gameObject, Box, Box);
            c._fill = Kit.Image(box, "Fill", null, Color.clear);
            Kit.Fill(c._fill.rectTransform);
            c._outline = Kit.Image(box, "Outline", null, Color.clear);
            Kit.Fill(c._outline.rectTransform);
            c._check = Kit.Image(box, "Check", Sprites.Check, Color.white);
            Kit.Fill(c._check.rectTransform, 1f, 1f, 1f, 1f);

            c._label = Kit.MakeText(rt, "Label", label, Theme.FontSize, Theme.Text, TextAnchor.MiddleLeft);
            c._label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            c._label.horizontalOverflow = HorizontalWrapMode.Wrap;   // long labels wrap and the row grows, not clipped
            c._label.verticalOverflow = VerticalWrapMode.Overflow;

            float s = value ? 1f : 0f;
            c._fill.rectTransform.localScale = new Vector3(s, s, 1f);
            c._check.rectTransform.localScale = new Vector3(s, s, 1f);
            c.Finish();
            return c;
        }

        protected override void ApplyTheme()
        {
            int r = Mathf.Min(Theme.RadiusSmall, 7);
            Kit.SetSprite(_fill, Sprites.Rounded(r));
            _fill.color = Theme.Accent;
            Kit.SetSprite(_outline, Sprites.Outline(r, 2));
            _outline.color = OutlineColor();
            _label.font = Kit.Font;
            _label.fontSize = Theme.FontSize;
            _label.color = Theme.Text;
        }

        private Color OutlineColor()
        {
            if (_on) return Kit.Alpha(Theme.Accent, 0f);
            return _hover ? Theme.TextDim : Kit.Alpha(Theme.TextDim, 0.55f);
        }

        public void SetValue(bool on, bool animate, bool notify)
        {
            if (on == _on && animate) return;
            _on = on;
            float s = on ? 1f : 0f;
            if (animate)
            {
                Tween.Scale(_fill.transform, s, on ? 0.3f : 0.15f, on ? Ease.OutBack : Ease.InQuad);
                Tween.Scale(_check.transform, s, on ? 0.35f : 0.12f, on ? Ease.OutBack : Ease.InQuad, null, on ? 0.06f : 0f);
                Tween.Tint(_outline, OutlineColor(), 0.15f, Ease.OutQuad);
            }
            else
            {
                _fill.transform.localScale = new Vector3(s, s, 1f);
                _check.transform.localScale = new Vector3(s, s, 1f);
                _outline.color = OutlineColor();
            }
            if (notify && OnChanged != null)
            {
                try { OnChanged(on); }
                catch (System.Exception ex) { Log.Write("KitCheckbox.OnChanged threw: " + ex); }
            }
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _hover = true;
            Tween.Tint(_outline, OutlineColor(), 0.15f, Ease.OutQuad);
        }

        public void OnPointerExit(PointerEventData e)
        {
            _hover = false;
            Tween.Tint(_outline, OutlineColor(), 0.2f, Ease.OutQuad);
        }

        public void OnPointerDown(PointerEventData e) { } // claims the press (see KitHover)

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) SetValue(!_on, true, true);
        }
    }
}
