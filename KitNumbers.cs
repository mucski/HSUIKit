using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace HSUIKit
{
    // Numeric text box with Blender-style scrubbing: drag horizontally on an attached handle
    // (the field's label, or an axis tag) to change the value. Shift = 10x, Ctrl = 0.1x.
    public class KitNumberInput : KitElement
    {
        public FloatHandler OnChanged;
        public float Step = 0.01f;       // value change per scrubbed pixel
        public float Min = float.NegativeInfinity;
        public float Max = float.PositiveInfinity;
        public string Format = "0.###";

        private KitInput _input;
        private float _value;
        private bool _whole;
        private bool _scrubbing;

        public float Value
        {
            get { return _value; }
            set { SetValue(value, false); }
        }

        public KitInput Input { get { return _input; } }

        public static KitNumberInput Create(Transform parent, float value, bool wholeNumbers, FloatHandler onChanged)
        {
            KitInput input = KitInput.Create(parent, "0", "", null);
            input.gameObject.name = "NumberInput";
            input.Field.contentType = wholeNumbers ? InputField.ContentType.IntegerNumber : InputField.ContentType.DecimalNumber;
            input.Field.characterLimit = 16;
            KitNumberInput n = input.gameObject.AddComponent<KitNumberInput>();
            n._input = input;
            n._whole = wholeNumbers;
            n.OnChanged = onChanged;
            if (wholeNumbers) { n.Step = 0.1f; n.Format = "0"; }
            input.OnSubmit = delegate(string s)
            {
                float v;
                if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) n.SetValue(v, true);
                n.Show();
            };
            n._value = value;
            n.Finish();
            n.Show();
            return n;
        }

        protected override void ApplyTheme() { }

        public void SetValue(float v, bool notify)
        {
            v = Mathf.Clamp(v, Min, Max);
            if (_whole) v = Mathf.Round(v);
            bool changed = !Mathf.Approximately(v, _value);
            _value = v;
            Show();
            if (notify && changed && OnChanged != null)
            {
                try { OnChanged(v); }
                catch (System.Exception ex) { Log.Write("KitNumberInput.OnChanged threw: " + ex); }
            }
        }

        private void Show()
        {
            if (!_input.Field.isFocused) _input.Text = _value.ToString(Format, CultureInfo.InvariantCulture);
        }

        // Scrubbing accumulates a float so whole-number fields still move smoothly per pixel.
        private float _scrubAccum;

        public void AttachScrub(GameObject handle, Graphic highlight)
        {
            AttachScrub(handle, highlight, true);
        }

        public void AttachScrub(GameObject handle, Graphic highlight, bool immediate)
        {
            Kit.HitArea(handle);
            KitScrub s = handle.AddComponent<KitScrub>();
            s.Immediate = immediate;
            s.OnHover = delegate(bool on)
            {
                if (highlight != null && !_scrubbing) Tween.Tint(highlight, on ? Theme.Accent : Theme.TextDim, 0.12f, Ease.OutQuad);
            };
            s.OnActive = delegate(bool on)
            {
                _scrubbing = on;
                _scrubAccum = _value;
                if (on) _input.Field.DeactivateInputField();
                Tween.Scale(_input.transform, on ? 1.03f : 1f, 0.2f, on ? Ease.OutCubic : Ease.OutBack);
                if (highlight != null) Tween.Tint(highlight, on ? Theme.Accent : Theme.TextDim, 0.12f, Ease.OutQuad);
            };
            s.OnScrub = delegate(float px)
            {
                _scrubAccum += px * Step;
                _scrubAccum = Mathf.Clamp(_scrubAccum, Min, Max);
                SetValue(_scrubAccum, true);
            };
        }
    }

    // "Label ........ [ 0.25 ]" with the label as the scrub handle.
    public class KitNumberField : KitElement
    {
        public KitNumberInput Number;
        private Text _label;

        public float Value
        {
            get { return Number.Value; }
            set { Number.Value = value; }
        }

        public static KitNumberField Create(Transform parent, string label, float value, bool wholeNumbers, FloatHandler onChanged)
        {
            RectTransform row = KitLayout.Row(parent, 10f);
            row.name = "NumberField " + label;
            Kit.Layout(row.gameObject, -1f, 34f);
            KitNumberField f = row.gameObject.AddComponent<KitNumberField>();

            RectTransform labelRt = Kit.Rect(row, "LabelHandle");
            labelRt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Kit.Layout(labelRt.gameObject, -1f, 34f);
            f._label = Kit.MakeText(labelRt, "Label", label, Theme.FontSize, Theme.TextDim, TextAnchor.MiddleLeft);
            Kit.Fill(f._label.rectTransform);

            f.Number = KitNumberInput.Create(row, value, wholeNumbers, onChanged);
            Kit.Layout(f.Number.gameObject, 120f, 34f);
            f.Number.AttachScrub(labelRt.gameObject, f._label);
            KitTooltip.Attach(labelRt.gameObject, "Drag left/right to scrub · Shift = fast · Ctrl = fine");
            f.Finish();
            return f;
        }

        protected override void ApplyTheme()
        {
            _label.font = Kit.Font;
            _label.fontSize = Theme.FontSize;
            _label.color = Theme.TextDim;
        }
    }

    // "Label  [X 0.0] [Y 0.0] [Z 0.0]" - each colored axis tag scrubs its own component.
    public class KitVectorField : KitElement
    {
        public Vector3Handler OnChanged;
        public KitNumberInput X, Y, Z;

        private Text _label;
        private int _axes = 3;
        private readonly Image[] _tags = new Image[3];
        private readonly Text[] _tagTexts = new Text[3];
        private static readonly string[] AxisHex = { "#F0616D", "#3DD68C", "#5B8CFF" };

        public Vector3 Value
        {
            get { return new Vector3(X.Value, Y.Value, Z.Value); }
            set { X.Value = value.x; Y.Value = value.y; Z.Value = value.z; }
        }

        public static KitVectorField Create(Transform parent, string label, Vector3 value, float step, Vector3Handler onChanged)
        {
            return Create(parent, label, value, step, onChanged, 3, 92f);
        }

        // axes = 2 gives an X/Y field (Z stays at value.z and is not shown).
        public static KitVectorField Create(Transform parent, string label, Vector3 value, float step, Vector3Handler onChanged, int axes, float labelWidth)
        {
            RectTransform row = KitLayout.Row(parent, 6f);
            row.name = "VectorField " + label;
            Kit.Layout(row.gameObject, -1f, 34f);
            KitVectorField f = row.gameObject.AddComponent<KitVectorField>();
            f.OnChanged = onChanged;

            f._label = Kit.MakeText(row, "Label", label, Theme.FontSize, Theme.Text, TextAnchor.MiddleLeft);
            Kit.Layout(f._label.gameObject, labelWidth, 34f);
            f._axes = Mathf.Clamp(axes, 1, 3);

            string[] axisNames = { "X", "Y", "Z" };
            float[] vals = { value.x, value.y, value.z };
            KitNumberInput[] inputs = new KitNumberInput[3];
            for (int i = 0; i < 3; i++)
            {
                RectTransform cell = Kit.Rect(row, "Cell " + axisNames[i]);
                cell.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                Kit.Layout(cell.gameObject, -1f, 34f);

                RectTransform tag = Kit.Rect(cell, "Tag");
                tag.anchorMin = new Vector2(0f, 0f);
                tag.anchorMax = new Vector2(0f, 1f);
                tag.pivot = new Vector2(0f, 0.5f);
                tag.sizeDelta = new Vector2(22f, 0f);
                tag.anchoredPosition = Vector2.zero;
                f._tags[i] = tag.gameObject.AddComponent<Image>();
                f._tagTexts[i] = Kit.MakeText(tag, "Axis", axisNames[i], Theme.SmallSize, Color.white, TextAnchor.MiddleCenter);
                f._tagTexts[i].fontStyle = FontStyle.Bold;
                Kit.Fill(f._tagTexts[i].rectTransform);

                KitNumberInput n = KitNumberInput.Create(cell, vals[i], false, delegate(float v) { f.Raise(); });
                n.Step = step;
                RectTransform nrt = (RectTransform)n.transform;
                Kit.Fill(nrt, 24f, 0f, 0f, 0f);
                n.AttachScrub(tag.gameObject, null);
                inputs[i] = n;
                if (i >= f._axes) cell.gameObject.SetActive(false);
            }
            f.X = inputs[0]; f.Y = inputs[1]; f.Z = inputs[2];
            f.Finish();
            return f;
        }

        private void Raise()
        {
            if (OnChanged == null) return;
            try { OnChanged(Value); }
            catch (System.Exception ex) { Log.Write("KitVectorField.OnChanged threw: " + ex); }
        }

        protected override void ApplyTheme()
        {
            _label.font = Kit.Font;
            _label.fontSize = Theme.FontSize;
            _label.color = Theme.Text;
            for (int i = 0; i < 3; i++)
            {
                Kit.SetSprite(_tags[i], Sprites.Rounded(Mathf.Min(Theme.RadiusSmall, 8)));
                _tags[i].color = Kit.Alpha(Theme.Hex(AxisHex[i]), 0.85f);
                _tagTexts[i].font = Kit.Font;
                _tagTexts[i].fontSize = Theme.SmallSize;
            }
        }
    }

    // Pill selector with a sliding thumb behind the active option (e.g. FK | IK | Both).
    public class KitSegmented : KitElement
    {
        public IntHandler OnChanged;

        private Image _track, _thumb;
        private RectTransform _thumbRt;
        private readonly System.Collections.Generic.List<RectTransform> _segs = new System.Collections.Generic.List<RectTransform>();
        private readonly System.Collections.Generic.List<Text> _labels = new System.Collections.Generic.List<Text>();
        private int _selected;
        private float _tx, _tw;
        private bool _placed;

        public int Selected
        {
            get { return _selected; }
            set { Select(value, false); }
        }

        public static KitSegmented Create(Transform parent, string[] options, int selected, IntHandler onChanged)
        {
            RectTransform rt = Kit.Rect(parent, "Segmented");
            Kit.Layout(rt.gameObject, -1f, 34f);
            KitSegmented s = rt.gameObject.AddComponent<KitSegmented>();
            s.OnChanged = onChanged;
            s._selected = Mathf.Clamp(selected, 0, options.Length - 1);
            s._track = rt.gameObject.AddComponent<Image>();
            s._track.raycastTarget = false;
            HorizontalLayoutGroup h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(3, 3, 3, 3);
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;

            s._thumb = Kit.Image(rt, "Thumb", null, Color.clear);
            s._thumbRt = s._thumb.rectTransform;
            Kit.IgnoreLayout(s._thumb.gameObject);
            s._thumbRt.anchorMin = new Vector2(0f, 0f);
            s._thumbRt.anchorMax = new Vector2(0f, 1f);
            s._thumbRt.pivot = new Vector2(0f, 0.5f);

            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                RectTransform seg = Kit.Rect(rt, "Seg " + options[i]);
                Kit.HitArea(seg.gameObject);
                Text t = Kit.MakeText(seg, "Label", options[i], Theme.FontSize, Theme.TextDim, TextAnchor.MiddleCenter);
                Kit.Fill(t.rectTransform);
                KitHoverClick hc = KitHoverClick.On(seg.gameObject);
                hc.OnEnter = delegate { if (index != s._selected) Tween.Tint(t, Theme.Text, 0.12f, Ease.OutQuad); };
                hc.OnExit = delegate { if (index != s._selected) Tween.Tint(t, Theme.TextDim, 0.18f, Ease.OutQuad); };
                hc.OnClick = delegate { s.Select(index, true); };
                s._segs.Add(seg);
                s._labels.Add(t);
            }
            s.Finish();
            return s;
        }

        protected override void ApplyTheme()
        {
            Kit.SetSprite(_track, Sprites.Rounded(Theme.RadiusSmall));
            _track.color = Theme.Surface;
            Kit.SetSprite(_thumb, Sprites.Rounded(Mathf.Max(0, Theme.RadiusSmall - 2)));
            _thumb.color = Theme.Accent;
            for (int i = 0; i < _labels.Count; i++)
            {
                _labels[i].font = Kit.Font;
                _labels[i].fontSize = Theme.FontSize;
                _labels[i].color = i == _selected ? Color.white : Theme.TextDim;
                _labels[i].fontStyle = i == _selected ? FontStyle.Bold : Theme.LabelStyle;
            }
        }

        public void Select(int index, bool notify)
        {
            if (index < 0 || index >= _segs.Count || index == _selected) return;
            _selected = index;
            for (int i = 0; i < _labels.Count; i++)
            {
                if (!notify) { Tween.Kill(_labels[i], "color"); _labels[i].color = i == index ? Color.white : Theme.TextDim; }
                else Tween.Tint(_labels[i], i == index ? Color.white : Theme.TextDim, 0.18f, Ease.OutQuad);
                _labels[i].fontStyle = i == index ? FontStyle.Bold : Theme.LabelStyle;
            }
            if (notify && OnChanged != null)
            {
                try { OnChanged(index); }
                catch (System.Exception ex) { Log.Write("KitSegmented.OnChanged threw: " + ex); }
            }
        }

        // Jump the thumb to the current option without gliding (e.g. after rebinding a pooled row).
        public void Snap()
        {
            _placed = false;
        }

        private void LateUpdate()
        {
            if (_segs.Count == 0) return;
            RectTransform seg = _segs[_selected];
            float w = seg.rect.width;
            if (w <= 0f) return;
            RectTransform root = (RectTransform)transform;
            float x = seg.localPosition.x + seg.rect.xMin - root.rect.xMin;
            if (!_placed) { _tx = x; _tw = w; _placed = true; }
            else
            {
                float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 16f * Mathf.Max(0.05f, Theme.AnimSpeed));
                _tx = Mathf.Lerp(_tx, x, k);
                _tw = Mathf.Lerp(_tw, w, k);
            }
            // Whole pixels: a gliding rounded shape at fractional positions blurs and shows slice seams.
            Vector2 p = new Vector2(Mathf.Round(_tx), 0f), s = new Vector2(Mathf.Round(_tw), -6f);
            if ((_thumbRt.anchoredPosition - p).sqrMagnitude > 0.0001f) _thumbRt.anchoredPosition = p;
            if ((_thumbRt.sizeDelta - s).sqrMagnitude > 0.0001f) _thumbRt.sizeDelta = s;
        }
    }
}
