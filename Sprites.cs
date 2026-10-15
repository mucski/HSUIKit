using System.Collections.Generic;
using UnityEngine;

namespace HSUIKit
{
    // Procedural sprites built from signed-distance functions - the whole kit ships with zero
    // image assets. Rounded shapes are tiny 9-sliced textures (1 sprite px = 1 canvas unit at
    // pixelsPerUnit 100), cached per radius so a theme radius change just builds new ones.
    //
    // Slicing rule: a sliced sprite with border b needs the target rect to be >= 2b on each
    // axis, so pills use Pill(height) = Rounded(height/2 - 1) rather than Rounded(height/2).
    public static class Sprites
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Rounded(int radius)
        {
            int r = Mathf.Max(0, radius);
            int size = 2 * r + 4;
            float half = size * 0.5f;
            return Build("round" + r, size, false, r + 1, delegate(float x, float y)
            {
                return 0.5f - RoundBox(x - half, y - half, half, half, r);
            });
        }

        public static Sprite Outline(int radius, int thickness)
        {
            int r = Mathf.Max(0, radius);
            int t = Mathf.Max(1, thickness);
            int border = Mathf.Max(r, t) + 1;
            int size = 2 * border + 2;
            float half = size * 0.5f;
            return Build("outline" + r + "_" + t, size, false, border, delegate(float x, float y)
            {
                float d = RoundBox(x - half, y - half, half, half, r);
                return Mathf.Clamp01(0.5f - d) - Mathf.Clamp01(0.5f - (d + t));
            });
        }

        // Soft drop shadow. Place the Image `blur` px larger than the casting rect on every side.
        public static Sprite Shadow(int radius, int blur)
        {
            int r = Mathf.Max(0, radius);
            int b = Mathf.Max(1, blur);
            int size = 2 * (r + b) + 4;
            float half = size * 0.5f;
            float inner = half - b;
            return Build("shadow" + r + "_" + b, size, false, r + b + 1, delegate(float x, float y)
            {
                float d = RoundBox(x - half, y - half, inner, inner, r);
                if (d <= 0f) return 1f;
                float f = Mathf.Clamp01(1f - d / b);
                return f * f;
            });
        }

        public static Sprite Pill(float height)
        {
            return Rounded(Mathf.Max(0, Mathf.FloorToInt(height * 0.5f) - 1));
        }

        public static Sprite Circle
        {
            get
            {
                return Build("circle", 64, true, 0, delegate(float x, float y)
                {
                    return 0.5f - (Length(x - 32f, y - 32f) - 31f);
                });
            }
        }

        // Radial soft shadow: solid core of radius 20, falloff to 32 (of a 64px texture).
        public static Sprite CircleShadow
        {
            get
            {
                return Build("circleshadow", 64, true, 0, delegate(float x, float y)
                {
                    float d = Length(x - 32f, y - 32f) - 20f;
                    if (d <= 0f) return 1f;
                    float f = Mathf.Clamp01(1f - d / 12f);
                    return f * f;
                });
            }
        }

        public static Sprite Check
        {
            get
            {
                return Build("check", 32, true, 0, delegate(float x, float y)
                {
                    float d = Mathf.Min(SegmentDistance(x, y, 7f, 16.5f, 13f, 10.5f),
                                        SegmentDistance(x, y, 13f, 10.5f, 25f, 22.5f));
                    return 2.3f - d;
                });
            }
        }

        // Points down (a "v").
        public static Sprite Chevron
        {
            get
            {
                return Build("chevron", 32, true, 0, delegate(float x, float y)
                {
                    float d = Mathf.Min(SegmentDistance(x, y, 9f, 20f, 16f, 13f),
                                        SegmentDistance(x, y, 16f, 13f, 23f, 20f));
                    return 2.1f - d;
                });
            }
        }

        public static Sprite Cross
        {
            get
            {
                return Build("cross", 32, true, 0, delegate(float x, float y)
                {
                    float d = Mathf.Min(SegmentDistance(x, y, 10f, 10f, 22f, 22f),
                                        SegmentDistance(x, y, 10f, 22f, 22f, 10f));
                    return 2.1f - d;
                });
            }
        }

        // 270-degree ring whose alpha ramps up toward its head - rotate it for a spinner.
        public static Sprite Arc
        {
            get
            {
                return Build("arc", 64, true, 0, delegate(float x, float y)
                {
                    float dx = x - 32f, dy = y - 32f;
                    float ring = Mathf.Clamp01(3.7f - Mathf.Abs(Length(dx, dy) - 26f));
                    float frac = Mathf.Atan2(dy, dx) / (2f * Mathf.PI);
                    if (frac < 0f) frac += 1f;
                    if (frac > 0.75f) return 0f;
                    return ring * (0.1f + 0.9f * (frac / 0.75f));
                });
            }
        }

        public static Sprite Plus
        {
            get
            {
                return Build("plus", 32, true, 0, delegate(float x, float y)
                {
                    float d = Mathf.Min(SegmentDistance(x, y, 16f, 8f, 16f, 24f), SegmentDistance(x, y, 8f, 16f, 24f, 16f));
                    return 2.2f - d;
                });
            }
        }

        // Rounded "play" triangle pointing right.
        public static Sprite Play
        {
            get
            {
                return Build("play", 32, true, 0, delegate(float x, float y)
                {
                    // Distance to a triangle (10,7)-(10,25)-(25,16), negative inside.
                    float d1 = SegmentDistance(x, y, 10f, 7f, 10f, 25f);
                    float d2 = SegmentDistance(x, y, 10f, 25f, 25f, 16f);
                    float d3 = SegmentDistance(x, y, 25f, 16f, 10f, 7f);
                    float d = Mathf.Min(d1, Mathf.Min(d2, d3));
                    bool inside = x > 10f && (y - 7f) > (x - 10f) * (9f / 15f) && (25f - y) > (x - 10f) * (9f / 15f);
                    return inside ? 1f : 1.2f - d;
                });
            }
        }

        // Magnifying glass.
        public static Sprite Search
        {
            get
            {
                return Build("search", 32, true, 0, delegate(float x, float y)
                {
                    float ring = Mathf.Abs(Length(x - 14f, y - 18f) - 7f);
                    float handle = SegmentDistance(x, y, 19.5f, 12.5f, 25f, 7f);
                    return 2.1f - Mathf.Min(ring, handle);
                });
            }
        }

        // Disc for KitRipple: radius 27 of a 64 px texture, so a 4+ px border is fully transparent,
        // and NO mipmaps. The ripple maps most of its area outside this texture (clamped); with
        // mips, the low mip levels' edge texels were partly opaque where the disc touched the edges,
        // and clamping smeared them into a big "+" at the start of every ripple.
        public const float RippleDiscFill = 27f / 32f; // disc radius as a fraction of the half-size

        public static Sprite RippleDisc
        {
            get
            {
                return Build("rippledisc", 64, false, 0, delegate(float x, float y)
                {
                    return 0.5f - (Length(x - 32f, y - 32f) - 27f);
                });
            }
        }

        // Counter-clockwise circular arrow ("reset to original").
        public static Sprite Reset
        {
            get
            {
                return Build("reset", 32, true, 0, delegate(float x, float y)
                {
                    float dx = x - 16f, dy = y - 16f;
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg; // -180..180, 0 = right
                    float ring = Mathf.Abs(Length(dx, dy) - 9f);
                    // Open gap at the upper right (between 20 and 80 degrees).
                    float arc = ang > 20f && ang < 80f ? 99f : ring;
                    // Arrowhead at the gap's 80-degree end, pointing counter-clockwise.
                    float hx = 16f + 9f * Mathf.Cos(80f * Mathf.Deg2Rad), hy = 16f + 9f * Mathf.Sin(80f * Mathf.Deg2Rad);
                    float head = Mathf.Min(SegmentDistance(x, y, hx, hy, hx + 6.5f, hy + 1.5f),
                                           SegmentDistance(x, y, hx, hy, hx + 2.5f, hy - 5.5f));
                    return 2.1f - Mathf.Min(arc, head);
                });
            }
        }

        // For plugin-specific icons (e.g. a toolbar button): white shape, tint with Image.color.
        public static Sprite Custom(string key, int size, AlphaFunc alpha)
        {
            return Build("custom_" + key, size, true, 0, alpha);
        }

        public static float RoundBox(float px, float py, float halfW, float halfH, float r)
        {
            float qx = Mathf.Abs(px) - (halfW - r);
            float qy = Mathf.Abs(py) - (halfH - r);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        public static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
        {
            float abx = bx - ax, aby = by - ay;
            float t = Mathf.Clamp01(((px - ax) * abx + (py - ay) * aby) / (abx * abx + aby * aby));
            return Length(px - (ax + abx * t), py - (ay + aby * t));
        }

        private static float Length(float x, float y)
        {
            return Mathf.Sqrt(x * x + y * y);
        }

        private static Sprite Build(string key, int size, bool mips, int border, AlphaFunc alpha)
        {
            return Build(key, size, size, mips, border, delegate(float x, float y)
            {
                return new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(x, y)));
            });
        }

        private static Sprite Build(string key, int w, int h, bool mips, int border, PixelFunc pixel)
        {
            Sprite cached;
            if (Cache.TryGetValue(key, out cached) && cached != null) return cached;

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, mips);
            tex.name = "HSUIKit_" + key;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;

            Color32[] px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    px[y * w + x] = pixel(x + 0.5f, y + 0.5f);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(mips, false);

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.name = tex.name;
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Cache[key] = sprite;
            return sprite;
        }

        // ---- color picker / window pieces ----

        // Circle outline (handles on the color picker).
        public static Sprite Ring
        {
            get
            {
                return Build("ring", 64, true, 0, delegate(float x, float y)
                {
                    float d = Length(x - 32f, y - 32f);
                    return Mathf.Clamp01(0.5f - (d - 31f)) - Mathf.Clamp01(0.5f - (d - 23f));
                });
            }
        }

        // Rounded w x h mask coverage at a pixel (AA), for baking fixed-size rounded textures.
        private static float Mask(float x, float y, int w, int h, float r)
        {
            return Mathf.Clamp01(0.5f - RoundBox(x - w * 0.5f, y - h * 0.5f, w * 0.5f, h * 0.5f, r));
        }

        // Full-saturation hue rainbow, left (0) to right (1), rounded.
        public static Sprite HueBar(int w, int h, int r)
        {
            return Build("huebar" + w + "x" + h + "_" + r, w, h, false, 0, delegate(float x, float y)
            {
                Color c = HsvToRgb(x / w, 1f, 1f);
                c.a = Mask(x, y, w, h, r);
                return c;
            });
        }

        // White with alpha 0 at left -> 1 at right, rounded. Tint for alpha / white-fade layers.
        public static Sprite FadeRight(int w, int h, int r, bool reverse)
        {
            return Build("faderight" + w + "x" + h + "_" + r + (reverse ? "r" : ""), w, h, false, 0, delegate(float x, float y)
            {
                float t = x / w;
                return new Color(1f, 1f, 1f, (reverse ? 1f - t : t) * Mask(x, y, w, h, r));
            });
        }

        // White with alpha 1 at bottom -> 0 at top, rounded. Tint black for the value layer.
        public static Sprite FadeUp(int w, int h, int r)
        {
            return Build("fadeup" + w + "x" + h + "_" + r, w, h, false, 0, delegate(float x, float y)
            {
                return new Color(1f, 1f, 1f, (1f - y / h) * Mask(x, y, w, h, r));
            });
        }

        // Transparency checkerboard, rounded, baked at its display size.
        public static Sprite Checker(int w, int h, int r)
        {
            return Build("checker" + w + "x" + h + "_" + r, w, h, false, 0, delegate(float x, float y)
            {
                bool odd = ((int)(x / 5f) + (int)(y / 5f)) % 2 == 1;
                float g = odd ? 0.62f : 0.82f;
                return new Color(g, g, g, Mask(x, y, w, h, r));
            });
        }

        // Three diagonal strokes for a window's bottom-right resize corner.
        public static Sprite ResizeGrip
        {
            get
            {
                return Build("resizegrip", 32, true, 0, delegate(float x, float y)
                {
                    // Stripes run "/" across the bottom-right corner (texture y is up), shortest nearest it.
                    float d = Mathf.Min(SegmentDistance(x, y, 8f, 6f, 26f, 24f),
                              Mathf.Min(SegmentDistance(x, y, 15f, 6f, 26f, 17f),
                                        SegmentDistance(x, y, 22f, 6f, 26f, 10f)));
                    return 1.6f - d;
                });
            }
        }

        public static Color HsvToRgb(float h, float s, float v)
        {
            h = (h % 1f + 1f) % 1f * 6f;
            int i = Mathf.FloorToInt(h) % 6;
            float f = h - Mathf.Floor(h);
            float p = v * (1f - s), q = v * (1f - s * f), t = v * (1f - s * (1f - f));
            switch (i)
            {
                case 0: return new Color(v, t, p);
                case 1: return new Color(q, v, p);
                case 2: return new Color(p, v, t);
                case 3: return new Color(p, q, v);
                case 4: return new Color(t, p, v);
                default: return new Color(v, p, q);
            }
        }

        public static void RgbToHsv(Color c, out float h, out float s, out float v)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            float d = max - min;
            v = max;
            s = max > 0f ? d / max : 0f;
            if (d <= 0f) { h = 0f; return; }
            if (max == c.r) h = ((c.g - c.b) / d) % 6f;
            else if (max == c.g) h = (c.b - c.r) / d + 2f;
            else h = (c.r - c.g) / d + 4f;
            h /= 6f;
            if (h < 0f) h += 1f;
        }
    }
}
