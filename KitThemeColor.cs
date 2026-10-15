using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HSUIKit
{
    // Keeps a graphic's colour following the theme: the function is re-applied on every theme switch.
    // Colours set once at build time otherwise stayed in the old theme until the game restarted (measured
    // 2026-10-11: 128 stale graphics in 8 windows after a Glass Dark -> Candy Light switch).
    //   KitThemeColor.Bind(listBg, () => Theme.ListBg);       explicit
    //   Kit.MakeText / Kit.Image bind automatically when given a theme colour (any alpha) - see AutoBind.
    // One static registry (not an event subscription per graphic): rows that a list activates and deactivates
    // while scrolling would otherwise re-subscribe every time.
    public class KitThemeColor : MonoBehaviour
    {
        private static readonly List<KitThemeColor> All = new List<KitThemeColor>();

        private Graphic _graphic;
        private System.Func<Color> _color;

        public static KitThemeColor Bind(Graphic graphic, System.Func<Color> color)
        {
            if (graphic == null || color == null) return null;
            KitThemeColor k = graphic.GetComponent<KitThemeColor>();
            if (k == null)
            {
                k = graphic.gameObject.AddComponent<KitThemeColor>();
                All.Add(k);
            }
            k._graphic = graphic;
            k._color = color;
            k.Apply();
            return k;
        }

        // Neutral theme colours a build-time colour can be recognised as. Accent/Error/Success/Warning are left
        // out on purpose: their Glass Dark values double as fixed literal colours (X/Y/Z axis red/green/blue,
        // colour-picker swatches), which must not change with the theme - bind those explicitly where wanted.
        private static readonly System.Func<Color>[] Tokens =
        {
            () => Theme.Text, () => Theme.TextDim, () => Theme.Surface, () => Theme.SurfaceHover, () => Theme.SurfaceActive,
            () => Theme.Card, () => Theme.Track, () => Theme.Background, () => Theme.Border,
        };

        // Binds `graphic` if `color` is a theme colour (same RGB, any alpha: Kit.Alpha(Theme.X, a) counts).
        // White, black and fully transparent colours are left alone - too likely to be meant literally.
        public static void AutoBind(Graphic graphic, Color color)
        {
            if (graphic == null || color.a <= 0.001f) return;
            if (Same(color, Color.white) || Same(color, Color.black)) return;
            for (int i = 0; i < Tokens.Length; i++)
            {
                Color t = Tokens[i]();
                if (!Same(color, t)) continue;
                System.Func<Color> token = Tokens[i];
                float ratio = t.a > 0.001f ? color.a / t.a : 1f;
                Bind(graphic, () => { Color c = token(); c.a = Mathf.Clamp01(c.a * ratio); return c; });
                return;
            }
        }

        private static bool Same(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f;
        }

        // Called by Theme.NotifyChanged before kit widgets restyle (they may override with state colours).
        internal static void ApplyAll()
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                if (All[i] == null) { All.RemoveAt(i); continue; }
                All[i].Apply();
            }
        }

        private void OnDestroy() { All.Remove(this); }

        private void Apply()
        {
            if (_graphic == null || _color == null) return;
            try { _graphic.color = _color(); }
            catch (System.Exception ex) { Log.Write("KitThemeColor: " + ex.Message); }
        }
    }
}
