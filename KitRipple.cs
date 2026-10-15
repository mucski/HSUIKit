using UnityEngine;

namespace HSUIKit
{
    // Click ripple that respects rounded corners. The ripple is a KitRoundedImage covering the
    // host with the host's own corner shape; the expanding circle is the circle texture mapped
    // through an animated uvRect, so it can never draw outside the rounded outline. (The old
    // version grew a circle Image inside a RectMask2D, whose square clip let the ripple fill the
    // corners outside the rounded shape.)
    public static class KitRipple
    {
        public static void Play(RectTransform host, int siblingIndex, Vector2 screen, float cornerRadius,
            bool roundTop, bool roundBottom, Color color, float duration = 0.55f)
        {
            RectTransform rt = Kit.Rect(host, "Ripple");
            Kit.Fill(rt);
            Kit.IgnoreLayout(rt.gameObject);
            if (siblingIndex >= 0) rt.SetSiblingIndex(siblingIndex);

            KitRoundedImage img = rt.gameObject.AddComponent<KitRoundedImage>();
            img.raycastTarget = false;
            img.texture = Sprites.RippleDisc.texture;
            img.Radius = cornerRadius;
            img.SetCorners(roundTop, roundBottom);
            img.color = color;

            Rect r = rt.rect;
            float w = Mathf.Max(1f, r.width), h = Mathf.Max(1f, r.height);
            Vector2 p = Kit.ScreenToLocal(rt, screen) - r.min; // from the bottom-left corner
            float endRadius = Mathf.Sqrt(w * w + h * h);      // reaches the farthest corner from anywhere
            SetCircle(img, p, 4f, w, h);

            Tween.Run(img, "ripple", duration, Ease.OutCubic, delegate(float t)
            {
                SetCircle(img, p, Mathf.Max(4f, Tween.Lerp(4f, endRadius, t)), w, h);
            });
            Tween.Run(img, "fade", duration, Ease.InQuad, delegate(float t)
            {
                img.color = Kit.Alpha(color, color.a * (1f - t));
            }, delegate { if (img != null) Object.Destroy(img.gameObject); });
        }

        // Maps the ripple disc (centred at uv 0.5, radius RippleDiscFill * 0.5) to a circle of
        // `radius` centred on p (host units), across a host of w x h.
        private static void SetCircle(KitRoundedImage img, Vector2 p, float radius, float w, float h)
        {
            float d = 2f * radius / Sprites.RippleDiscFill; // host units per 1.0 of uv
            img.uvRect = new Rect(0.5f - p.x / d, 0.5f - p.y / d, w / d, h / d);
        }
    }
}
