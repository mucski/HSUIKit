using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HSUIKit
{
    // Label + value readout on top, pill track below. Handle grows on hover; while dragging a
    // halo expands behind it and a value bubble pops up above it.
    public class KitSlider : KitElement, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        IInitializePotentialDragHandler, IDragHandler
    {
        private const float HandleSize = 16f;
        private const float TrackH = 6f;

        public FloatHandler OnChanged;
        public FloatHandler OnReleased; // fired once when a drag/click ends
        public string Format = "0.00";
        public string Suffix = "";

        private float _min, _max, _value;
        private bool _whole;
        private float _visual; // 0..1 position actually drawn
        private bool _hover, _dragging;

        private Text _label, _valueText, _bubbleText;
        private RectTransform _track, _fillRt, _handle, _halo, _bubble;
        private Image _trackImg, _fill, _handleImg, _handleDot, _haloImg, _bubbleImg;
        private CanvasGroup _bubbleGroup;
        private float _bubbleLift;   // extra rise while the bubble fades in (px)

        public float Value
        {
            get { return _value; }
            set { SetValue(value, true, false); }
        }

        public bool Dragging { get { return _dragging; } }

        public static KitSlider Create(Transform parent, string label, float min, float max, float value, bool wholeNumbers, FloatHandler onChanged)
        {
            // label == null builds a compact slider: just the track (28 px), no label/value row.
            bool compact = label == null;
            RectTransform root = Kit.Rect(parent, "Slider " + (label ?? "compact"));
            Kit.Layout(root.gameObject, -1f, compact ? 28f : 46f);

            Text lbl = Kit.MakeText(root, "Label", label ?? "", Theme.FontSize, Theme.Text, TextAnchor.MiddleLeft);
            lbl.rectTransform.anchorMin = new Vector2(0f, 1f);
            lbl.rectTransform.anchorMax = new Vector2(1f, 1f);
            lbl.rectTransform.pivot = new Vector2(0.5f, 1f);
            lbl.rectTransform.sizeDelta = new Vector2(0f, 20f);
            lbl.rectTransform.anchoredPosition = Vector2.zero;

            Text val = Kit.MakeText(root, "Value", "", Theme.FontSize, Theme.TextDim, TextAnchor.MiddleRight);
            val.rectTransform.anchorMin = new Vector2(0f, 1f);
            val.rectTransform.anchorMax = new Vector2(1f, 1f);
            val.rectTransform.pivot = new Vector2(0.5f, 1f);
            val.rectTransform.sizeDelta = new Vector2(0f, 20f);
            val.rectTransform.anchoredPosition = Vector2.zero;

            // Hit area = the whole lower strip, taller than the visible track for easy grabbing.
            RectTransform hit = Kit.Rect(root, "Hit");
            hit.anchorMin = new Vector2(0f, 0f);
            hit.anchorMax = new Vector2(1f, 0f);
            hit.pivot = new Vector2(0.5f, 0f);
            hit.sizeDelta = new Vector2(0f, 24f);
            hit.anchoredPosition = compact ? new Vector2(0f, 2f) : Vector2.zero;
            Kit.HitArea(hit.gameObject);
            if (compact)
            {
                lbl.gameObject.SetActive(false);
                val.gameObject.SetActive(false);
            }

            KitSlider s = hit.gameObject.AddComponent<KitSlider>();
            s._label = lbl;
            s._valueText = val;
            s._min = min;
            s._max = max;
            s._whole = wholeNumbers;
            s.OnChanged = onChanged;
            if (wholeNumbers) s.Format = "0";

            s._trackImg = Kit.Image(hit, "Track", null, Color.clear);
            s._track = s._trackImg.rectTransform;
            s._track.anchorMin = new Vector2(0f, 0.5f);
            s._track.anchorMax = new Vector2(1f, 0.5f);
            s._track.pivot = new Vector2(0.5f, 0.5f);
            s._track.sizeDelta = new Vector2(-HandleSize, TrackH);
            s._track.anchoredPosition = Vector2.zero;

            s._fill = Kit.Image(s._track, "Fill", null, Color.clear);
            s._fillRt = s._fill.rectTransform;
            s._fillRt.anchorMin = Vector2.zero;
            s._fillRt.pivot = new Vector2(0f, 0.5f);
            s._fillRt.offsetMin = Vector2.zero;
            s._fillRt.offsetMax = Vector2.zero;

            s._haloImg = Kit.Image(s._track, "Halo", Sprites.Circle, Color.clear);
            s._halo = s._haloImg.rectTransform;
            s._halo.sizeDelta = new Vector2(32f, 32f);
            s._halo.localScale = Vector3.zero;

            s._handleImg = Kit.Image(s._track, "Handle", Sprites.Circle, Color.white);
            s._handle = s._handleImg.rectTransform;
            s._handle.sizeDelta = new Vector2(HandleSize, HandleSize);
            s._handleDot = Kit.Image(s._handle, "Dot", Sprites.Circle, Color.white);
            Kit.Anchor(s._handleDot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));

            // The value bubble lives on the kit's top layer and follows the handle: as the handle's child it
            // was clipped by any mask around the slider (foldout bodies, scroll views).
            s._bubbleImg = Kit.Image(Kit.TooltipLayer, "SliderBubble", null, Color.clear);
            s._bubble = s._bubbleImg.rectTransform;
            Kit.Anchor(s._bubble, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(40f, 22f));
            s._bubbleGroup = s._bubble.gameObject.AddComponent<CanvasGroup>();
            s._bubbleGroup.alpha = 0f;
            s._bubbleGroup.blocksRaycasts = false;
            s._bubble.gameObject.SetActive(false);
            s._bubbleText = Kit.MakeText(s._bubble, "Text", "", Theme.SmallSize, Color.white, TextAnchor.MiddleCenter);
            Kit.Fill(s._bubbleText.rectTransform);

            s._value = Mathf.Clamp(value, min, max);
            s._visual = s.ToT(s._value);
            s.Finish();
            s.Draw();
            return s;
        }

        protected override void ApplyTheme()
        {
            _label.font = Kit.Font; _label.fontSize = Theme.FontSize; _label.color = Theme.Text;
            _valueText.font = Kit.Font; _valueText.fontSize = Theme.FontSize; _valueText.color = Theme.TextDim;
            _bubbleText.font = Kit.Font; _bubbleText.fontSize = Theme.SmallSize;
            Kit.SetSprite(_trackImg, Sprites.Pill(TrackH));
            _trackImg.color = Theme.Track;
            Kit.SetSprite(_fill, Sprites.Pill(TrackH));
            _fill.color = Theme.Accent;
            _haloImg.color = Kit.Alpha(Theme.Accent, 0.25f);
            // Glass Dark: plain white knob. Candy themes: accent knob with a white centre dot, so it
            // never disappears into a light background.
            _handleImg.color = Theme.PillButtons ? Theme.Accent : Color.white;
            _handleDot.enabled = Theme.PillButtons;
            Kit.SetSprite(_bubbleImg, Sprites.Rounded(Mathf.Min(Theme.RadiusSmall, 9)));
            _bubbleImg.color = Theme.Accent;
        }

        private float ToT(float v)
        {
            return _max > _min ? Mathf.Clamp01((v - _min) / (_max - _min)) : 0f;
        }

        private string ValueString()
        {
            return _value.ToString(Format, CultureInfo.InvariantCulture) + Suffix;
        }

        private void Draw()
        {
            float t = Mathf.Clamp01(_visual);
            _fillRt.anchorMax = new Vector2(t, 1f);
            _fill.enabled = t * _track.rect.width >= TrackH || _track.rect.width <= 0f;
            Vector2 a = new Vector2(t, 0.5f);
            _handle.anchorMin = a; _handle.anchorMax = a; _handle.anchoredPosition = Vector2.zero;
            _halo.anchorMin = a; _halo.anchorMax = a; _halo.anchoredPosition = Vector2.zero;
            string s = ValueString();
            _valueText.text = s;
            _bubbleText.text = s;
            _bubble.sizeDelta = new Vector2(Mathf.Max(34f, _bubbleText.preferredWidth + 16f), 22f);
        }

        // Change the range (e.g. when a pooled row is rebound to another property).
        public void SetRange(float min, float max)
        {
            if (min == _min && (max > min ? max : min + 1f) == _max)
                return; // callers set it every frame; Draw builds strings
            _min = min;
            _max = max > min ? max : min + 1f;
            _value = Mathf.Clamp(_value, _min, _max);
            Tween.Kill(this, "visual");
            _visual = ToT(_value);
            Draw();
        }

        // Track width is 0 until the first layout pass; redraw once it's real.
        private void OnRectTransformDimensionsChange()
        {
            if (Built) Draw();
        }

        public void SetValue(float v, bool animate, bool notify)
        {
            v = Mathf.Clamp(v, _min, _max);
            if (_whole) v = Mathf.Round(v);
            bool changed = !Mathf.Approximately(v, _value);
            _value = v;
            float target = ToT(v);
            // nothing to show: same value, already drawn there (callers re-set the live value every frame; an
            // out-of-range value clamps and never "matched", so Draw ran - with its strings - every frame)
            if (!changed && !_dragging && Mathf.Approximately(_visual, target))
                return;
            if (animate && !_dragging)
            {
                float from = _visual;
                Tween.Run(this, "visual", 0.35f, Ease.OutCubic, delegate(float t) { _visual = Tween.Lerp(from, target, t); Draw(); });
            }
            else
            {
                Tween.Kill(this, "visual");
                _visual = target;
                Draw();
            }
            if (notify && changed && OnChanged != null)
            {
                try { OnChanged(v); }
                catch (System.Exception ex) { Log.Write("KitSlider.OnChanged threw: " + ex); }
            }
        }

        private void FromPointer(PointerEventData e)
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_track, e.position, e.pressEventCamera, out local)) return;
            Rect r = _track.rect;
            float t = r.width > 0f ? Mathf.Clamp01((local.x - r.xMin) / r.width) : 0f;
            SetValue(Tween.Lerp(_min, _max, t), false, true);
        }

        private void HandleScale()
        {
            float s = _dragging ? 1.2f : (_hover ? 1.12f : 1f);
            Tween.Scale(_handle, s, 0.2f, Ease.OutCubic);
        }

        public void OnPointerEnter(PointerEventData e) { _hover = true; HandleScale(); }

        public void OnPointerExit(PointerEventData e) { _hover = false; HandleScale(); }

        public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold = false; }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            _dragging = true;
            HandleScale();
            Tween.Scale(_halo, 1f, 0.3f, Ease.OutBack);
            _bubble.gameObject.SetActive(true);
            _bubble.SetAsLastSibling();
            Tween.Fade(_bubbleGroup, 1f, 0.15f, Ease.OutQuad);
            float lift0 = _bubbleGroup.alpha > 0.01f ? _bubbleLift : -6f;
            Tween.Run(this, "bubbleLift", 0.2f, Ease.OutCubic, delegate(float t) { _bubbleLift = Mathf.Lerp(lift0, 0f, t); });
            FromPointer(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (_dragging) FromPointer(e);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_dragging) return;
            _dragging = false;
            HandleScale();
            if (OnReleased != null)
            {
                try { OnReleased(_value); }
                catch (System.Exception ex) { Log.Write("KitSlider.OnReleased threw: " + ex); }
            }
            Tween.Scale(_halo, 0f, 0.2f, Ease.InQuad);
            Tween.Fade(_bubbleGroup, 0f, 0.18f, Ease.InQuad, delegate { if (_bubble != null && !_dragging) _bubble.gameObject.SetActive(false); }, 0.25f);
        }

        // Follow the handle (top centre + 8 px), on whole pixels so the bubble and its text stay crisp.
        private void LateUpdate()
        {
            if (_bubble == null || !_bubble.gameObject.activeSelf) return;
            Vector3[] c = Corners;
            _handle.GetWorldCorners(c);
            float s = Kit.ScaleFactor > 0.0001f ? Kit.ScaleFactor : 1f;
            Vector3 p = new Vector3((c[1].x + c[2].x) * 0.5f, c[1].y + (8f + _bubbleLift) * s, 0f);
            float w = _bubble.rect.width * s;
            p.x = Mathf.Round(p.x - w * 0.5f) + w * 0.5f;
            p.y = Mathf.Round(p.y);
            if ((_bubble.position - p).sqrMagnitude > 0.0001f) _bubble.position = p;
        }

        private static readonly Vector3[] Corners = new Vector3[4];

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_bubble != null) { _bubbleGroup.alpha = 0f; _bubble.gameObject.SetActive(false); }
        }

        private void OnDestroy()
        {
            if (_bubble != null) Destroy(_bubble.gameObject);
        }
    }
}
