using UnityEngine;
using UnityEngine.UI;

namespace HSUIKit
{
    public class KitProgress : KitElement
    {
        private Image _track, _fill, _segment;
        private RectTransform _fillRt, _segmentRt, _trackRt;
        private float _height;
        private float _value, _visual;
        private bool _indeterminate;
        private float _phase;

        public float Value
        {
            get { return _value; }
            set { SetValue(value, true); }
        }

        public bool Indeterminate
        {
            get { return _indeterminate; }
            set
            {
                _indeterminate = value;
                _segment.enabled = value;
                _fill.enabled = !value && _visual > 0.001f;
                _phase = 0f;
            }
        }

        public static KitProgress Create(Transform parent, float height = 8f)
        {
            RectTransform rt = Kit.Rect(parent, "Progress");
            Kit.Layout(rt.gameObject, -1f, height);
            KitProgress p = rt.gameObject.AddComponent<KitProgress>();
            p._height = height;
            p._track = rt.gameObject.AddComponent<Image>();
            p._track.raycastTarget = false;
            p._trackRt = rt;
            rt.gameObject.AddComponent<RectMask2D>();

            p._fill = Kit.Image(rt, "Fill", null, Color.clear);
            p._fillRt = p._fill.rectTransform;
            p._fillRt.anchorMin = Vector2.zero;
            p._fillRt.anchorMax = new Vector2(0f, 1f);
            p._fillRt.offsetMin = Vector2.zero;
            p._fillRt.offsetMax = Vector2.zero;
            p._fill.enabled = false;

            p._segment = Kit.Image(rt, "Segment", null, Color.clear);
            p._segmentRt = p._segment.rectTransform;
            p._segmentRt.anchorMin = Vector2.zero;
            p._segmentRt.anchorMax = new Vector2(0f, 1f);
            p._segmentRt.pivot = new Vector2(0f, 0.5f);
            p._segment.enabled = false;

            p.Finish();
            return p;
        }

        protected override void ApplyTheme()
        {
            Kit.SetSprite(_track, Sprites.Pill(_height));
            _track.color = Theme.Track;
            Kit.SetSprite(_fill, Sprites.Pill(_height));
            _fill.color = Theme.Accent;
            Kit.SetSprite(_segment, Sprites.Pill(_height));
            _segment.color = Theme.Accent;
        }

        public void SetValue(float v, bool animate)
        {
            _value = Mathf.Clamp01(v);
            if (animate)
            {
                float from = _visual, to = _value;
                Tween.Run(this, "value", 0.4f, Ease.OutCubic, delegate(float t) { Draw(Tween.Lerp(from, to, t)); });
            }
            else
            {
                Tween.Kill(this, "value");
                Draw(_value);
            }
        }

        private void Draw(float v)
        {
            _visual = v;
            _fillRt.anchorMax = new Vector2(Mathf.Clamp01(v), 1f);
            // A sliced pill narrower than its own height would draw its caps overlapping.
            _fill.enabled = !_indeterminate && v * _trackRt.rect.width >= _height;
        }

        private void Update()
        {
            if (!_indeterminate) return;
            float w = _trackRt.rect.width;
            if (w <= 0f) return;
            _phase = (_phase + Time.unscaledDeltaTime * 0.7f * Mathf.Max(0.05f, Theme.AnimSpeed)) % 1f;
            float e = Tween.Evaluate(Ease.InOutCubic, _phase);
            float segW = w * 0.32f;
            _segmentRt.sizeDelta = new Vector2(segW, 0f);
            _segmentRt.anchoredPosition = new Vector2(Tween.Lerp(-segW, w, e), 0f);
        }
    }

    public class KitSpinner : KitElement
    {
        private Image _img;
        public float Speed = 1f;

        public static KitSpinner Create(Transform parent, float size = 22f)
        {
            RectTransform rt = Kit.Rect(parent, "Spinner");
            Kit.Layout(rt.gameObject, size, size);
            KitSpinner s = rt.gameObject.AddComponent<KitSpinner>();
            RectTransform inner = Kit.Rect(rt, "Arc");
            Kit.Fill(inner);
            s._img = inner.gameObject.AddComponent<Image>();
            s._img.sprite = Sprites.Arc;
            s._img.raycastTarget = false;
            s.Finish();
            return s;
        }

        protected override void ApplyTheme()
        {
            _img.color = Theme.Accent;
        }

        private void Update()
        {
            _img.transform.Rotate(0f, 0f, 300f * Speed * Mathf.Max(0.05f, Theme.AnimSpeed) * Time.unscaledDeltaTime);
        }
    }
}
