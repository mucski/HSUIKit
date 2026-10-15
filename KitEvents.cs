using UnityEngine;
using UnityEngine.EventSystems;

namespace HSUIKit
{
    // Generic pointer relay for simple interactive pieces (tab items, list rows, headers).
    public class KitHoverClick : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public VoidHandler OnEnter;
        public VoidHandler OnExit;
        public VoidHandler OnDown;
        public VoidHandler OnUp;
        public VoidHandler OnClick;

        public static KitHoverClick On(GameObject go)
        {
            KitHoverClick h = go.GetComponent<KitHoverClick>();
            if (h == null) h = go.AddComponent<KitHoverClick>();
            return h;
        }

        public void OnPointerEnter(PointerEventData e) { if (OnEnter != null) OnEnter(); }
        public void OnPointerExit(PointerEventData e) { if (OnExit != null) OnExit(); }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left && OnDown != null) OnDown(); }
        public void OnPointerUp(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left && OnUp != null) OnUp(); }
        public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left && OnClick != null) OnClick(); }
    }

    // Hover-only relay for CONTAINERS (rows, cards). It deliberately has no pointer-down/click
    // interfaces: UGUI gives a click to the first object up the hierarchy that handles the
    // press, so a container with KitHoverClick steals clicks from click-only children (a row
    // killed its own dropdown this way).
    public class KitHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public VoidHandler OnEnter;
        public VoidHandler OnExit;

        public static KitHover On(GameObject go)
        {
            KitHover h = go.GetComponent<KitHover>();
            if (h == null) h = go.AddComponent<KitHover>();
            return h;
        }

        public void OnPointerEnter(PointerEventData e) { if (OnEnter != null) OnEnter(); }
        public void OnPointerExit(PointerEventData e) { if (OnExit != null) OnExit(); }
    }

    // Only one popup (dropdown list, etc.) is open at a time; windows close it on drag/hide.
    public static class KitPopup
    {
        private static VoidHandler _close;

        public static void Open(VoidHandler close)
        {
            CloseAll();
            _close = close;
        }

        public static void Forget(VoidHandler close)
        {
            if (_close == close) _close = null;
        }

        public static void CloseAll()
        {
            VoidHandler c = _close;
            _close = null;
            if (c != null) c();
        }

        // Full-screen invisible catcher in the popup layer: any mouse button pressed outside
        // the popup (which is created after it, so sits on top) closes it.
        public static RectTransform CreateBlocker(VoidHandler onDown)
        {
            RectTransform b = Kit.Rect(Kit.PopupLayer, "PopupBlocker");
            Kit.Fill(b);
            Kit.HitArea(b.gameObject);
            b.gameObject.AddComponent<KitBlocker>().OnDown = onDown;
            return b;
        }

        // Sizes + positions a popup under `anchor` (or above it when there's no room below),
        // left-aligned with it and clamped inside the screen. Returns true if it opened upward.
        public static bool PlaceNear(RectTransform popup, RectTransform anchor, Vector2 size, float gap)
        {
            RectTransform layer = Kit.PopupLayer;
            Vector3[] c = new Vector3[4];
            anchor.GetWorldCorners(c);
            Vector2 bottomLeft = Kit.WorldToLocal(layer, c[0]);
            Vector2 topLeft = Kit.WorldToLocal(layer, c[1]);
            Rect lr = layer.rect;
            float roomBelow = bottomLeft.y - lr.yMin;
            bool up = roomBelow < size.y + gap + 6f && (lr.yMax - topLeft.y) > roomBelow;
            float x = Mathf.Clamp(bottomLeft.x, lr.xMin + 6f, lr.xMax - size.x - 6f);
            Vector2 pos = up ? new Vector2(x, topLeft.y + gap) : new Vector2(x, bottomLeft.y - gap);
            Kit.Anchor(popup, new Vector2(0.5f, 0.5f), new Vector2(0f, up ? 0f : 1f), pos, size);
            return up;
        }

        // Positions a popup with its corner at a screen point (context menus), flipping left/up
        // when it would leave the screen. The pivot ends up at the corner touching the cursor.
        public static void PlaceAtPoint(RectTransform popup, Vector2 screen, Vector2 size)
        {
            RectTransform layer = Kit.PopupLayer;
            Vector2 p = Kit.ScreenToLocal(layer, screen);
            Rect lr = layer.rect;
            bool left = p.x + size.x > lr.xMax - 4f;
            bool up = p.y - size.y < lr.yMin + 4f;
            Kit.Anchor(popup, new Vector2(0.5f, 0.5f), new Vector2(left ? 1f : 0f, up ? 0f : 1f), p, size);
        }
    }

    public class KitBlocker : MonoBehaviour, IPointerDownHandler
    {
        public VoidHandler OnDown;

        public void OnPointerDown(PointerEventData e)
        {
            if (OnDown != null) OnDown();
        }
    }

    // Press-and-drag area reporting the pointer as normalized 0..1 inside its own rect
    // (color picker square/bars). Fires on press too, so a click jumps straight there.
    public class KitDragArea : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IInitializePotentialDragHandler, IDragHandler
    {
        public PointHandler OnPoint;
        public VoidHandler OnRelease;

        public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold = false; }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) Report(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) Report(e);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && OnRelease != null) OnRelease();
        }

        private void Report(PointerEventData e)
        {
            RectTransform rt = (RectTransform)transform;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out local)) return;
            Rect r = rt.rect;
            float x = r.width > 0f ? Mathf.Clamp01((local.x - r.xMin) / r.width) : 0f;
            float y = r.height > 0f ? Mathf.Clamp01((local.y - r.yMin) / r.height) : 0f;
            if (OnPoint != null) OnPoint(x, y);
        }
    }

    // Blender-style value scrubbing: horizontal drag reports pixel deltas (Shift = x10, Ctrl = x0.1).
    public class KitScrub : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public FloatHandler OnScrub;
        public BoolHandler OnHover;
        public BoolHandler OnActive;
        // true = scrubbing starts on the first pixel. false keeps Unity's drag threshold, so a
        // plain click on the same object still counts as a click (e.g. a label that also selects).
        public bool Immediate = true;

        private bool _dragging, _hover;

        public void OnPointerEnter(PointerEventData e) { _hover = true; if (OnHover != null) OnHover(true); }

        public void OnPointerExit(PointerEventData e) { _hover = false; if (!_dragging && OnHover != null) OnHover(false); }

        public void OnPointerDown(PointerEventData e) { } // claims the press so clicks route here

        public void OnInitializePotentialDrag(PointerEventData e) { if (Immediate) e.useDragThreshold = false; }

        public void OnBeginDrag(PointerEventData e)
        {
            _dragging = true;
            if (OnActive != null) OnActive(true);
        }

        public void OnDrag(PointerEventData e)
        {
            float mod = 1f;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) mod = 10f;
            else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) mod = 0.1f;
            if (OnScrub != null) OnScrub(e.delta.x / Kit.ScaleFactor * mod);
        }

        public void OnEndDrag(PointerEventData e)
        {
            _dragging = false;
            if (OnActive != null) OnActive(false);
            if (!_hover && OnHover != null) OnHover(false);
        }
    }
}
