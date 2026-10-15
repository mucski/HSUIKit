using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HSUIKit
{
    public enum Ease
    {
        Linear,
        InQuad, OutQuad, InOutQuad,
        InCubic, OutCubic, InOutCubic,
        OutQuart, OutQuint,
        OutExpo, InOutExpo,
        OutCirc,
        InBack, OutBack, InOutBack,
        OutElastic, OutBounce, Spring
    }

    // Minimal tween engine. One tween per (owner, channel): starting a new one on the same
    // channel replaces the old one WITHOUT firing its done callback, so e.g. a Show() that
    // interrupts a Hide() never gets deactivated by the stale hide's completion. Runs on
    // unscaled time (keeps animating when the game is paused) and is empty = free when idle.
    public static class Tween
    {
        private sealed class Data
        {
            public UnityEngine.Object Owner;
            public int OwnerId;
            public string Channel;
            public float Delay;
            public float Duration;
            public float Elapsed;
            public Ease Ease;
            public TweenSetter Setter;
            public VoidHandler Done;
            public bool Dead;
            public bool HasTintTo;  // Tint: where this tween ends (lets Tint skip identical requests)
            public Color TintTo;
        }

        private static readonly List<Data> Active = new List<Data>();

        public static int ActiveCount { get { return Active.Count; } }

        public static void Run(UnityEngine.Object owner, string channel, float duration, Ease ease,
            TweenSetter setter, VoidHandler done = null, float delay = 0f)
        {
            if (owner == null || setter == null) return;
            Kit.Ensure();
            Kill(owner, channel);

            float speed = Mathf.Max(0.05f, Theme.AnimSpeed);
            duration /= speed;
            delay /= speed;

            if (duration <= 0f && delay <= 0f)
            {
                setter(1f);
                if (done != null) done();
                return;
            }

            Data d = new Data();
            d.Owner = owner;
            d.OwnerId = owner.GetInstanceID();
            d.Channel = channel;
            d.Delay = delay;
            d.Duration = duration;
            d.Ease = ease;
            d.Setter = setter;
            d.Done = done;
            Active.Add(d);
        }

        public static void Kill(UnityEngine.Object owner, string channel)
        {
            if (owner == null) return;
            int id = owner.GetInstanceID();
            for (int i = 0; i < Active.Count; i++)
            {
                Data d = Active[i];
                if (!d.Dead && d.OwnerId == id && d.Channel == channel) d.Dead = true;
            }
        }

        public static void KillAll(UnityEngine.Object owner)
        {
            if (owner == null) return;
            int id = owner.GetInstanceID();
            for (int i = 0; i < Active.Count; i++)
            {
                if (Active[i].OwnerId == id) Active[i].Dead = true;
            }
        }

        internal static void Tick(float dt)
        {
            // Snapshot the count: tweens started from inside a callback land past it and
            // begin next frame, which is the behavior callers expect anyway.
            int count = Active.Count;
            for (int i = 0; i < count; i++)
            {
                Data d = Active[i];
                if (d.Dead) continue;
                if (d.Owner == null) { d.Dead = true; continue; }

                if (d.Delay > 0f)
                {
                    d.Delay -= dt;
                    if (d.Delay > 0f) continue;
                }

                d.Elapsed += dt;
                float t = d.Duration <= 0f ? 1f : Mathf.Clamp01(d.Elapsed / d.Duration);
                try
                {
                    d.Setter(Evaluate(d.Ease, t));
                }
                catch (Exception ex)
                {
                    Log.Write("Tween setter threw (" + d.Channel + "): " + ex);
                    d.Dead = true;
                    continue;
                }

                if (t >= 1f)
                {
                    d.Dead = true;
                    if (d.Done != null)
                    {
                        try { d.Done(); }
                        catch (Exception ex) { Log.Write("Tween done threw (" + d.Channel + "): " + ex); }
                    }
                }
            }

            for (int i = Active.Count - 1; i >= 0; i--)
            {
                if (Active[i].Dead) Active.RemoveAt(i);
            }
        }

        public static float Evaluate(Ease ease, float t)
        {
            switch (ease)
            {
                case Ease.InQuad: return t * t;
                case Ease.OutQuad: return 1f - (1f - t) * (1f - t);
                case Ease.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                case Ease.InCubic: return t * t * t;
                case Ease.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                case Ease.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
                case Ease.OutQuart: return 1f - Mathf.Pow(1f - t, 4f);
                case Ease.OutQuint: return 1f - Mathf.Pow(1f - t, 5f);
                case Ease.OutExpo: return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
                case Ease.InOutExpo:
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return t < 0.5f ? Mathf.Pow(2f, 20f * t - 10f) / 2f : (2f - Mathf.Pow(2f, -20f * t + 10f)) / 2f;
                case Ease.OutCirc: return Mathf.Sqrt(1f - Mathf.Pow(t - 1f, 2f));
                case Ease.InBack:
                {
                    const float c1 = 1.70158f;
                    return (c1 + 1f) * t * t * t - c1 * t * t;
                }
                case Ease.OutBack:
                {
                    const float c1 = 1.70158f;
                    float u = t - 1f;
                    return 1f + (c1 + 1f) * u * u * u + c1 * u * u;
                }
                case Ease.InOutBack:
                {
                    const float c2 = 1.70158f * 1.525f;
                    return t < 0.5f
                        ? (Mathf.Pow(2f * t, 2f) * ((c2 + 1f) * 2f * t - c2)) / 2f
                        : (Mathf.Pow(2f * t - 2f, 2f) * ((c2 + 1f) * (t * 2f - 2f) + c2) + 2f) / 2f;
                }
                case Ease.OutElastic:
                {
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    const float c4 = (2f * Mathf.PI) / 3f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
                }
                case Ease.OutBounce: return Bounce(t);
                case Ease.Spring:
                    if (t >= 1f) return 1f;
                    return 1f - Mathf.Exp(-6f * t) * Mathf.Cos(t * 4.5f * Mathf.PI);
                default: return t;
            }
        }

        private static float Bounce(float t)
        {
            const float n1 = 7.5625f, d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
            if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }

        // ---- convenience helpers (from-value captured at call time) ----

        public static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { return a + (b - a) * t; }

        public static Color Lerp(Color a, Color b, float t)
        {
            return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
        }

        public static void Fade(CanvasGroup group, float to, float duration, Ease ease, VoidHandler done = null, float delay = 0f)
        {
            if (group == null) return;
            float from = group.alpha;
            Run(group, "alpha", duration, ease, delegate(float t) { group.alpha = Mathf.Clamp01(Lerp(from, to, t)); }, done, delay);
        }

        public static void Scale(Transform target, Vector3 to, float duration, Ease ease, VoidHandler done = null, float delay = 0f)
        {
            if (target == null) return;
            Vector3 from = target.localScale;
            // Text snapped to the pixel grid mid-animation is off-grid at the final scale: land exactly on
            // `to` and, when that's full size, re-snap the labels underneath.
            VoidHandler finish = delegate
            {
                target.localScale = to;
                if (to.x == 1f && to.y == 1f) KitTextSnap.Resnap(target);
                if (done != null) done();
            };
            Run(target, "scale", duration, ease, delegate(float t) { target.localScale = from + (to - from) * t; }, finish, delay);
        }

        public static void Scale(Transform target, float to, float duration, Ease ease, VoidHandler done = null, float delay = 0f)
        {
            Scale(target, new Vector3(to, to, 1f), duration, ease, done, delay);
        }

        // Slides from (resting position + offset) back to the resting position in WHOLE screen pixels.
        // Panels animate with this instead of a scale pop: a scaled or sub-pixel panel puts its rounded
        // shapes' slice lines between pixels (dashed seams under the game's MSAA) and blurs its text.
        public static void SlideIn(RectTransform target, Vector2 offset, float duration, Ease ease, VoidHandler done = null)
        {
            if (target == null) return;
            float s = Kit.ScaleFactor > 0.0001f ? Kit.ScaleFactor : 1f;
            // Rest with the bottom-left corner on a whole pixel (overlay canvas: world units = pixels).
            Vector3[] c = new Vector3[4];
            target.GetWorldCorners(c);
            Vector2 rest = target.anchoredPosition + new Vector2(Mathf.Round(c[0].x) - c[0].x, Mathf.Round(c[0].y) - c[0].y) / s;
            Run(target, "slide", duration, ease, delegate(float t)
            {
                Vector2 o = offset * (1f - t);
                target.anchoredPosition = rest + new Vector2(Mathf.Round(o.x * s) / s, Mathf.Round(o.y * s) / s);
            }, delegate
            {
                // Snap again at rest: a freshly built panel's layout (size) can settle after the first frame.
                target.anchoredPosition = rest;
                target.GetWorldCorners(c);
                target.anchoredPosition = rest + new Vector2(Mathf.Round(c[0].x) - c[0].x, Mathf.Round(c[0].y) - c[0].y) / s;
                if (done != null) done();
            });
            target.anchoredPosition = rest + new Vector2(Mathf.Round(offset.x * s) / s, Mathf.Round(offset.y * s) / s);
        }

        public static void Move(RectTransform target, Vector2 to, float duration, Ease ease, VoidHandler done = null, float delay = 0f)
        {
            if (target == null) return;
            Vector2 from = target.anchoredPosition;
            Run(target, "move", duration, ease, delegate(float t) { target.anchoredPosition = Lerp(from, to, t); }, done, delay);
        }

        public static void MoveX(RectTransform target, float to, float duration, Ease ease, VoidHandler done = null, float delay = 0f)
        {
            if (target == null) return;
            float from = target.anchoredPosition.x;
            Run(target, "moveX", duration, ease, delegate(float t)
            {
                Vector2 p = target.anchoredPosition; p.x = Lerp(from, to, t); target.anchoredPosition = p;
            }, done, delay);
        }

        public static void MoveY(RectTransform target, float to, float duration, Ease ease, VoidHandler done = null, float delay = 0f)
        {
            if (target == null) return;
            float from = target.anchoredPosition.y;
            Run(target, "moveY", duration, ease, delegate(float t)
            {
                Vector2 p = target.anchoredPosition; p.y = Lerp(from, to, t); target.anchoredPosition = p;
            }, done, delay);
        }

        public static void Size(RectTransform target, Vector2 to, float duration, Ease ease, VoidHandler done = null, float delay = 0f)
        {
            if (target == null) return;
            Vector2 from = target.sizeDelta;
            Run(target, "size", duration, ease, delegate(float t) { target.sizeDelta = Lerp(from, to, t); }, done, delay);
        }

        public static void Rotate(Transform target, float toZ, float duration, Ease ease, VoidHandler done = null, float delay = 0f)
        {
            if (target == null) return;
            float from = target.localEulerAngles.z;
            if (from > 180f) from -= 360f;
            Run(target, "rotate", duration, ease, delegate(float t) { target.localEulerAngles = new Vector3(0f, 0f, Lerp(from, toZ, t)); }, done, delay);
        }

        public static void Tint(Graphic graphic, Color to, float duration, Ease ease, VoidHandler done = null, float delay = 0f)
        {
            if (graphic == null) return;
            // Repaint paths (list rows, buttons) call this with the colour they already have, often several times a
            // second; each Run allocates a Data + closure + delegate (~150 B). Skip when there is nothing to do: already
            // heading to that colour, or already there with no tween running.
            if (done == null && delay <= 0f)
            {
                int id = graphic.GetInstanceID();
                bool running = false;
                for (int i = 0; i < Active.Count; i++)
                {
                    Data d = Active[i];
                    if (d.Dead || d.OwnerId != id || d.Channel != "color") continue;
                    running = true;
                    if (d.HasTintTo && d.TintTo == to) return;
                }
                if (!running && graphic.color == to) return;
            }
            Color from = graphic.color;
            Run(graphic, "color", duration, ease, delegate(float t) { graphic.color = Lerp(from, to, Mathf.Clamp01(t)); }, done, delay);
            Data last = Active.Count != 0 ? Active[Active.Count - 1] : null;
            if (last != null && last.OwnerId == graphic.GetInstanceID() && last.Channel == "color")
            {
                last.HasTintTo = true;
                last.TintTo = to;
            }
        }
    }
}
