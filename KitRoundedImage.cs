using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HSUIKit
{
    // RawImage that draws itself as a rounded rectangle: the mesh is a triangle fan with arc
    // corners plus a 1 px alpha fringe for anti-aliasing. Works on any background, unlike the old
    // "paint the corners in the background color" overlay (which showed as squares wherever the
    // real background differed - hover tints, nested rows). No stencil Mask involved either.
    // With no texture it draws a plain rounded shape in its color.
    public class KitRoundedImage : RawImage
    {
        private const int Segments = 6;
        private const float Fringe = 0.5f; // half-width of the soft edge, canvas units

        private float _radius = 6f;
        private bool _roundTop = true, _roundBottom = true;

        private static readonly List<Vector2> Points = new List<Vector2>();
        private static readonly List<Vector2> Normals = new List<Vector2>();

        public float Radius
        {
            get { return _radius; }
            set
            {
                if (Mathf.Approximately(_radius, value)) return;
                _radius = value;
                SetVerticesDirty();
            }
        }

        // Which corners are rounded (e.g. a card's image: top only, the caption meets its bottom).
        public void SetCorners(bool top, bool bottom)
        {
            if (_roundTop == top && _roundBottom == bottom) return;
            _roundTop = top;
            _roundBottom = bottom;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0f || r.height <= 0f) return;
            float rad = Mathf.Clamp(_radius, 0f, Mathf.Min(r.width, r.height) * 0.5f);

            // Outline counter-clockwise from the bottom-right corner.
            Points.Clear();
            Normals.Clear();
            float rb = _roundBottom ? rad : 0f, rt = _roundTop ? rad : 0f;
            Corner(r.xMax - rb, r.yMin + rb, rb, 270f);
            Corner(r.xMax - rt, r.yMax - rt, rt, 0f);
            Corner(r.xMin + rt, r.yMax - rt, rt, 90f);
            Corner(r.xMin + rb, r.yMin + rb, rb, 180f);

            Color32 solid = color;
            Color32 clear = solid;
            clear.a = 0;
            Rect uv = uvRect;
            int n = Points.Count;

            vh.AddVert(new Vector3(r.center.x, r.center.y), solid, UV(r.center, r, uv));
            for (int i = 0; i < n; i++)
            {
                Vector2 p = Points[i] - Normals[i] * Fringe;
                vh.AddVert(new Vector3(p.x, p.y), solid, UV(p, r, uv));
            }
            for (int i = 0; i < n; i++)
            {
                Vector2 p = Points[i] + Normals[i] * Fringe;
                vh.AddVert(new Vector3(p.x, p.y), clear, UV(p, r, uv));
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                vh.AddTriangle(0, 1 + i, 1 + j);
                vh.AddTriangle(1 + i, 1 + n + i, 1 + n + j);
                vh.AddTriangle(1 + i, 1 + n + j, 1 + j);
            }
        }

        // Adds one corner: an arc of `radius` around (cx, cy) starting at `startDeg`, or a single
        // point (with a diagonal normal) for a square corner.
        private static void Corner(float cx, float cy, float radius, float startDeg)
        {
            if (radius <= 0f)
            {
                float mid = (startDeg + 45f) * Mathf.Deg2Rad;
                Points.Add(new Vector2(cx, cy));
                Normals.Add(new Vector2(Mathf.Cos(mid), Mathf.Sin(mid)) * 1.4142f);
                return;
            }
            for (int k = 0; k <= Segments; k++)
            {
                float a = (startDeg + 90f * k / Segments) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Points.Add(new Vector2(cx, cy) + dir * radius);
                Normals.Add(dir);
            }
        }

        private static Vector2 UV(Vector2 p, Rect r, Rect uv)
        {
            float u = Mathf.Clamp01((p.x - r.xMin) / r.width);
            float v = Mathf.Clamp01((p.y - r.yMin) / r.height);
            return new Vector2(uv.x + u * uv.width, uv.y + v * uv.height);
        }
    }
}
