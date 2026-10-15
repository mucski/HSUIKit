using UnityEngine;
using UnityEngine.UI;

namespace HSUIKit
{
    // Drives a GridLayoutGroup made by KitLayout.FlexGrid: re-fits the cells whenever the grid's width
    // or item count changes (window resize, items added), so rows end flush instead of leaving a gap.
    public class KitGridFit : MonoBehaviour
    {
        public Vector2 MinImage = new Vector2(100f, 100f);
        public float Caption;
        public float MaxGrow = 1.5f;

        private GridLayoutGroup _grid;
        private RectTransform _rt;
        private float _lastWidth = -1f;
        private int _lastCount = -1;

        private void LateUpdate()
        {
            if (_grid == null) { _grid = GetComponent<GridLayoutGroup>(); _rt = (RectTransform)transform; }
            float width = _rt.rect.width;
            int count = transform.childCount;
            if (width == _lastWidth && count == _lastCount) return;
            _lastWidth = width;
            _lastCount = count;
            Fit(width, count);
        }

        private void Fit(float width, int count)
        {
            float avail = width - _grid.padding.left - _grid.padding.right;
            float sp = _grid.spacing.x;
            if (avail <= 0f || MinImage.x <= 0f) return;
            int cols = Mathf.Max(1, Mathf.FloorToInt((avail + sp) / (MinImage.x + sp)));
            if (count > 0 && count < cols) cols = count;   // one short row: let it fill too
            float cell = Mathf.Floor((avail - sp * (cols - 1)) / cols);
            cell = Mathf.Min(cell, Mathf.Floor(MinImage.x * MaxGrow));
            float imageH = Mathf.Round(cell * MinImage.y / MinImage.x);
            _grid.cellSize = new Vector2(cell, imageH + Caption);
            for (int i = 0; i < transform.childCount; i++)
            {
                KitImageButton b = transform.GetChild(i).GetComponent<KitImageButton>();
                if (b != null) b.Resize(new Vector2(cell, imageH));
            }
        }
    }
}
