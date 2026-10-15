using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace HSUIKit
{
    // Dev tool: measures a built UI and logs layout problems - overlapping siblings, children spilling
    // out of their parent, text that doesn't fit its box - plus the vertical rhythm (gap between each
    // pair of stacked items) so spacing can be checked by numbers instead of by eye.
    // Usage: KitLayoutAudit.Run(window.Content, "MyPlugin/Settings") after the page is built and shown.
    public static class KitLayoutAudit
    {
        private static readonly Vector3[] A = new Vector3[4];
        private static readonly Vector3[] B = new Vector3[4];

        public static int Run(RectTransform root, string label)
        {
            if (root == null) return 0;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            int issues = 0;
            Walk(root, root, label, ref issues);
            Log.Write("audit[" + label + "]: " + issues + " issue(s)");
            return issues;
        }

        private static void Walk(RectTransform rt, RectTransform root, string label, ref int issues)
        {
            if (!rt.gameObject.activeInHierarchy) return;
            if (rt.GetComponent<RectMask2D>() != null || rt.GetComponent<ScrollRect>() != null) { WalkChildren(rt, root, label, ref issues); return; }
            LayoutGroup group = rt.GetComponent<LayoutGroup>();
            List<RectTransform> laid = new List<RectTransform>();
            for (int i = 0; i < rt.childCount; i++)
            {
                RectTransform c = rt.GetChild(i) as RectTransform;
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                if (group != null && !Ignored(c)) laid.Add(c);
                // Spill: a laid-out child poking out of its parent (decorations that ignore layout are allowed).
                if (group != null && !Ignored(c) && Visible(c))
                {
                    Rect p = World(rt, A), r = World(c, B);
                    float over = Mathf.Max(p.xMin - r.xMin, r.xMax - p.xMax);
                    if (over > 1f) { issues++; Log.Write("audit[" + label + "]: SPILL " + Path(c, root) + " sticks out of its parent by " + over.ToString("0") + " px"); }
                }
                // Clipped sideways: anything drawn that the nearest mask cuts off (scroll viewports clip vertically
                // by design). Checked for every element that draws, laid out or hand-placed.
                Graphic own = c.GetComponent<Graphic>();
                if (own != null && own.enabled && own.color.a > 0.01f)
                {
                    RectMask2D mask = c.GetComponentInParent<RectMask2D>();
                    if (mask != null)
                    {
                        Rect m = World(mask.rectTransform, A), r = World(c, B);
                        float cut = Mathf.Max(m.xMin - r.xMin, r.xMax - m.xMax);
                        if (cut > 1f) { issues++; Log.Write("audit[" + label + "]: CLIPPED " + Path(c, root) + " is cut off " + cut.ToString("0") + " px by " + mask.name + " (width " + m.width.ToString("0") + ")"); }
                    }
                }
                Text t = c.GetComponent<Text>();
                if (t != null && t.enabled && !string.IsNullOrEmpty(t.text)) CheckText(t, root, label, ref issues);
            }
            // Overlap between laid-out siblings.
            for (int i = 0; i < laid.Count; i++)
                for (int j = i + 1; j < laid.Count; j++)
                {
                    Rect a = World(laid[i], A), b = World(laid[j], B);
                    float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
                    float h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                    if (w > 1f && h > 1f) { issues++; Log.Write("audit[" + label + "]: OVERLAP " + Path(laid[i], root) + " x " + laid[j].name + " (" + w.ToString("0") + "x" + h.ToString("0") + " px)"); }
                }
            GridLayoutGroup grid = group as GridLayoutGroup;
            if (grid != null)
                Log.Write("audit[" + label + "]: GRID " + Path(rt, root) + " width " + rt.rect.width.ToString("0.#") + " cell " + grid.cellSize.x.ToString("0.#") + "x" + grid.cellSize.y.ToString("0.#") + " spacing " + grid.spacing.x.ToString("0.#") + " pad " + grid.padding.left + "/" + grid.padding.right + " items " + laid.Count);
            // Vertical rhythm of stacks.
            if (group is VerticalLayoutGroup && laid.Count > 1)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 1; i < laid.Count; i++)
                {
                    Rect prev = World(laid[i - 1], A), cur = World(laid[i], B);
                    sb.Append(Short(laid[i - 1])).Append(" ").Append((prev.yMin - cur.yMax).ToString("0")).Append(" ");
                }
                sb.Append(Short(laid[laid.Count - 1]));
                Rect box = World(rt, A), first = World(laid[0], B), last = World(laid[laid.Count - 1], B);
                Log.Write("audit[" + label + "]: GAPS " + Path(rt, root) + " pad top " + (box.yMax - first.yMax).ToString("0") + " bottom " + (last.yMin - box.yMin).ToString("0") + ": " + sb);
            }
            WalkChildren(rt, root, label, ref issues);
        }

        // Dev: logs the world rect of every object under `root` whose path contains `match` (and its children),
        // plus the nearest mask, for "why is this cut off" questions the audit can't see.
        public static void DumpRects(RectTransform root, string match)
        {
            foreach (RectTransform t in root.GetComponentsInChildren<RectTransform>(true))
            {
                string p = Path(t, root);
                if (p.IndexOf(match, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                Rect r = World(t, A);
                RectMask2D mask = t.GetComponentInParent<RectMask2D>();
                string m = mask != null ? World(mask.rectTransform, B).ToString() : "-";
                Log.Write("dump: " + p + " active " + t.gameObject.activeInHierarchy + " x " + r.xMin.ToString("0.#") + ".." + r.xMax.ToString("0.#") + " w " + r.width.ToString("0.#") + " mask " + m);
            }
        }

        private static void WalkChildren(RectTransform rt, RectTransform root, string label, ref int issues)
        {
            for (int i = 0; i < rt.childCount; i++)
            {
                RectTransform c = rt.GetChild(i) as RectTransform;
                if (c != null) Walk(c, root, label, ref issues);
            }
        }

        private static void CheckText(Text t, RectTransform root, string label, ref int issues)
        {
            Rect r = t.rectTransform.rect;
            TextGenerationSettings s = t.GetGenerationSettings(new Vector2(r.width, 0f));
            if (t.horizontalOverflow == HorizontalWrapMode.Overflow)
            {
                float w = t.cachedTextGeneratorForLayout.GetPreferredWidth(t.text, s) / t.pixelsPerUnit;
                if (w > r.width + 2f && r.width > 0f) { issues++; Log.Write("audit[" + label + "]: TEXT-WIDE " + Path(t.rectTransform, root) + " '" + Clip(t.text) + "' needs " + w.ToString("0") + " px, box " + r.width.ToString("0")); }
            }
            float h = t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text, s) / t.pixelsPerUnit;
            if (h > r.height + 1f && r.height > 0f) { issues++; Log.Write("audit[" + label + "]: TEXT-TALL " + Path(t.rectTransform, root) + " '" + Clip(t.text) + "' needs " + h.ToString("0") + " px, box " + r.height.ToString("0")); }
        }

        private static bool Ignored(RectTransform c)
        {
            ILayoutIgnorer ig = c.GetComponent(typeof(ILayoutIgnorer)) as ILayoutIgnorer;
            return ig != null && ig.ignoreLayout;
        }

        private static bool Visible(RectTransform c)
        {
            Graphic g = c.GetComponentInChildren<Graphic>();
            return g != null && g.enabled;
        }

        private static Rect World(RectTransform rt, Vector3[] c)
        {
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        private static string Path(Transform t, Transform root)
        {
            string p = t.name;
            for (Transform x = t.parent; x != null && x != root; x = x.parent) p = x.name + "/" + p;
            return p;
        }

        private static string Short(RectTransform c)
        {
            Text t = c.GetComponent<Text>();
            if (t != null) return "'" + Clip(t.text) + "'";
            Text inner = c.GetComponentInChildren<Text>();
            return c.name + (inner != null ? "('" + Clip(inner.text) + "')" : "");
        }

        private static string Clip(string s)
        {
            s = s.Replace("\n", " ");
            return s.Length > 18 ? s.Substring(0, 18) + "~" : s;
        }
    }
}
