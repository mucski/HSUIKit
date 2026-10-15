using UnityEngine;
using UnityEngine.UI;

namespace HSUIKit
{
    // Moves a Text's glyph quads so they start on the screen-pixel grid. Labels centred in boxes
    // whose height doesn't divide evenly around the line land half a pixel off (21 of 27 showcase
    // labels did), and Unity then smears every glyph across two pixel rows - text looked blurry next
    // to other UIs. Canvas.pixelPerfect did NOT fix it (measured). Glyph positions are integer
    // relative to each other, so snapping the first vertex snaps them all.
    // Runs whenever the text rebuilds; Resnap() re-runs it after something rescales (open animations).
    public class KitTextSnap : BaseMeshEffect
    {
        private static readonly System.Collections.Generic.List<KitTextSnap> Found = new System.Collections.Generic.List<KitTextSnap>();

        private KitWindow _window;

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            _window = null;
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            UIVertex v = new UIVertex();
            vh.PopulateUIVertex(ref v, 0);
            Vector3 d;
            if (_window == null) _window = GetComponentInParent<KitWindow>();
            if (_window != null)
            {
                // Measured from the window's corner in its own (unanimated) space: windows rest on whole
                // pixels, so this is right at full size even when built mid open-animation - no
                // half-pixel jump when the animation ends.
                RectTransform wr = _window.Rect;
                Vector3 lp = wr.InverseTransformPoint(transform.TransformPoint(v.position));
                float s = Kit.ScaleFactor;
                float px = (lp.x - wr.rect.xMin) * s, py = (lp.y - wr.rect.yMin) * s;
                float dx = Mathf.Round(px) - px, dy = Mathf.Round(py) - py;
                if (Mathf.Abs(dx) < 0.01f && Mathf.Abs(dy) < 0.01f) return;
                d = transform.InverseTransformVector(wr.TransformVector(new Vector3(dx / s, dy / s, 0f)));
            }
            else
            {
                Vector3 w = transform.TransformPoint(v.position);
                float dx = Mathf.Round(w.x) - w.x, dy = Mathf.Round(w.y) - w.y;
                if (Mathf.Abs(dx) < 0.01f && Mathf.Abs(dy) < 0.01f) return;
                d = transform.InverseTransformVector(new Vector3(dx, dy, 0f));
            }
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                v.position += d;
                vh.SetUIVertex(v, i);
            }
        }

        // Rebuild every snapped label under root (call once a scale/position animation settles).
        public static void Resnap(Transform root)
        {
            if (root == null) return;
            Found.Clear();
            root.GetComponentsInChildren(false, Found);
            for (int i = 0; i < Found.Count; i++)
            {
                if (Found[i].graphic != null) Found[i].graphic.SetVerticesDirty();
            }
            Found.Clear();
        }
    }
}
