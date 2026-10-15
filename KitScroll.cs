using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HSUIKit
{
    // Vertical scroll view: RectMask2D viewport (never stencil Mask - broken by this game's
    // pipeline, see HSDebugUI), slim themed scrollbar, and eased mouse-wheel scrolling instead
    // of ScrollRect's instant jumps. Content is a VerticalLayoutGroup + ContentSizeFitter stack.
    public class KitScroll : KitElement, IScrollHandler, IBeginDragHandler
    {
        public const float WheelStep = 70f;
        public const float Gutter = 14f;

        public RectTransform Content;
        public ScrollRect ScrollRect;

        private RectTransform _viewport;
        private Image _handle;
        private float _target;
        private bool _animating;
        private GameObject _bar;

        public static KitScroll Create(Transform parent, int padH = 18, int padV = 14, float spacing = 10f, bool scrollbar = true, bool layoutContent = true)
        {
            RectTransform rt = Kit.Rect(parent, "Scroll");
            Kit.Fill(rt);
            KitScroll k = rt.gameObject.AddComponent<KitScroll>();

            // With a scrollbar the viewport stops before a fixed gutter (bar 6 px + 4 px edge + 4 px gap), so the
            // bar never sits on top of the content. The gutter stays when the bar hides, so nothing reflows.
            k._viewport = Kit.Rect(rt, "Viewport");
            Kit.Fill(k._viewport, 0f, 0f, scrollbar ? Gutter : 0f, 0f);
            Kit.HitArea(k._viewport.gameObject);
            k._viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = Kit.Rect(k._viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
            if (layoutContent)
            {
                VerticalLayoutGroup v = content.gameObject.AddComponent<VerticalLayoutGroup>();
                // The gutter already spaces the right side: keep the visible margins roughly even.
                v.padding = new RectOffset(padH, scrollbar ? Mathf.Max(0, padH - (int)Gutter + 4) : padH, padV, padV);
                v.spacing = spacing;
                v.childForceExpandWidth = true;
                v.childForceExpandHeight = false; // must be false with a ContentSizeFitter (HSDebugUI bug #1)
                ContentSizeFitter fit = content.gameObject.AddComponent<ContentSizeFitter>();
                fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            k.Content = content;

            ScrollRect sr = rt.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.viewport = k._viewport;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 0f; // wheel handled by OnScroll below
            sr.inertia = true;
            k.ScrollRect = sr;

            if (scrollbar)
            {
                RectTransform barRt = Kit.Rect(rt, "Scrollbar");
                k._bar = barRt.gameObject;
                barRt.anchorMin = new Vector2(1f, 0f);
                barRt.anchorMax = new Vector2(1f, 1f);
                barRt.pivot = new Vector2(1f, 0.5f);
                barRt.sizeDelta = new Vector2(6f, -16f);
                barRt.anchoredPosition = new Vector2(-4f, 0f);
                Kit.HitArea(barRt.gameObject);
                RectTransform area = Kit.Rect(barRt, "Sliding Area");
                Kit.Fill(area);
                k._handle = Kit.Image(area, "Handle", null, Color.clear, true);
                Kit.Fill(k._handle.rectTransform);
                Scrollbar sb = barRt.gameObject.AddComponent<Scrollbar>();
                sb.handleRect = k._handle.rectTransform;
                sb.targetGraphic = k._handle;
                sb.direction = Scrollbar.Direction.BottomToTop;
                sb.transition = Selectable.Transition.None;
                Navigation nav = sb.navigation;
                nav.mode = Navigation.Mode.None;
                sb.navigation = nav;
                sr.verticalScrollbar = sb;
            }

            k.Finish();
            return k;
        }

        protected override void ApplyTheme()
        {
            if (_handle != null)
            {
                Kit.SetSprite(_handle, Sprites.Pill(6f));
                _handle.color = Kit.Alpha(Theme.TextDim, 0.35f);
            }
        }

        public void OnScroll(PointerEventData e)
        {
            float max = Mathf.Max(0f, Content.rect.height - _viewport.rect.height);
            if (!_animating) _target = Content.anchoredPosition.y;
            _target = PixelTarget(Mathf.Clamp(_target - e.scrollDelta.y * WheelStep, 0f, max));
            _animating = true;
            ScrollRect.StopMovement();
        }

        public void OnBeginDrag(PointerEventData e)
        {
            _animating = false;
        }

        public float ViewportHeight { get { return _viewport.rect.height; } }

        public void ScrollToTop()
        {
            _animating = false;
            Content.anchoredPosition = Vector2.zero;
        }

        // Glides just far enough that `child` (anything inside Content) is fully visible, with a small margin.
        public void ScrollIntoView(RectTransform child)
        {
            if (child == null) return;
            Vector3[] c = new Vector3[4];
            child.GetWorldCorners(c);
            float top = Content.rect.yMax - Content.InverseTransformPoint(c[1]).y;
            float bottom = Content.rect.yMax - Content.InverseTransformPoint(c[0]).y;
            float view = _viewport.rect.height;
            float current = _animating ? _target : Content.anchoredPosition.y;
            float max = Mathf.Max(0f, Content.rect.height - view);
            float t = current;
            if (top - 8f < current) t = top - 8f;
            else if (bottom + 8f > current + view) t = bottom + 8f - view;
            _target = PixelTarget(Mathf.Clamp(t, 0f, max));
            _animating = true;
            ScrollRect.StopMovement();
        }

        private void LateUpdate()
        {
            // a scrollbar only when there is something to scroll: a full-height handle on a short page looked broken
            if (_bar != null)
            {
                bool need = Content.rect.height > _viewport.rect.height + 1f;
                if (_bar.activeSelf != need)
                    _bar.SetActive(need);
            }
            if (!_animating)
            {
                // content got shorter (filtered, rows removed) while scrolled down: ScrollRect's Clamped mode
                // only clamps during a drag / wheel, so glide back to the new end instead of showing blank space
                float max = Mathf.Max(0f, Content.rect.height - _viewport.rect.height);
                if (Content.anchoredPosition.y > max + 0.5f && !Input.GetMouseButton(0))
                {
                    _target = PixelTarget(max);
                    _animating = true;
                    ScrollRect.StopMovement();
                }
                SnapContent();
                return;
            }
            Vector2 p = Content.anchoredPosition;
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 16f);
            p.y = Mathf.Lerp(p.y, _target, k);
            if (Mathf.Abs(p.y - _target) < 0.5f)
            {
                p.y = _target;
                _animating = false;
            }
            Content.anchoredPosition = p;
        }

        // Glide targets are whole pixels, so a finished glide lands on the grid by itself. Rounding the
        // position every frame instead stalled the glide ~4 px short (at ~500 fps each step is < 0.5 px,
        // which rounded back) and rewrote it every frame - a canvas rebuild per frame and a creeping page.
        private float PixelTarget(float y)
        {
            float s = Content.lossyScale.y;
            if (s <= 0.0001f) return y;
            float maxPx = Mathf.Floor(Mathf.Max(0f, Content.rect.height - _viewport.rect.height) * s);
            return Mathf.Clamp(Mathf.Round(y * s), 0f, maxPx) / s;
        }

        // Whole-pixel scroll offsets only: a fractional offset moves every (pixel-snapped) label in the
        // page half a pixel off the grid and blurs it. Skipped while dragged / still gliding by inertia.
        private void SnapContent()
        {
            if (Input.GetMouseButton(0) || ScrollRect.velocity.sqrMagnitude > 1f) return;
            float s = Content.lossyScale.y;
            if (s <= 0.0001f) return;
            Vector2 p = Content.anchoredPosition;
            // Stay inside the scroll range: rounding past the end (fractional max) made ScrollRect clamp it
            // back every frame and the page twitched. Floor the end instead.
            float maxPx = Mathf.Floor(Mathf.Max(0f, Content.rect.height - _viewport.rect.height) * s);
            float y = Mathf.Clamp(Mathf.Round(p.y * s), 0f, maxPx) / s;
            if (Mathf.Abs(y - p.y) > 0.001f) Content.anchoredPosition = new Vector2(p.x, y);
        }
    }
}
