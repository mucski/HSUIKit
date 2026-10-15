using System;
using System.Reflection;
using UnityEngine;

namespace HSUIKit
{
    // Keeps the game camera still while the mouse is used on a kit window or popup. Studio's camera already
    // ignores clicks on UI; the main game's and the chara maker's do not, they only stop when their
    // NoCtrlCondition delegate says so. Found by name (no reference to the game's code), re-applied every
    // frame because the chara maker's own panels replace that delegate on every click; whatever was set
    // before is kept and still asked.
    internal sealed class KitCameraGuard : MonoBehaviour
    {
        private const string FieldName = "NoCtrlCondition";
        private static readonly MethodInfo _ourMethod = typeof(KitCameraGuard).GetMethod("NoCtrl", BindingFlags.NonPublic | BindingFlags.Instance);

        private bool _block;
        private Camera _camera;
        private Component _control;
        private FieldInfo _field;
        private Delegate _ours;
        private BoolFunc _previous;

        private delegate bool BoolFunc();

        private void Update()
        {
            // Idle (no press that started over the kit) costs only these mouse checks: the camera lookup and
            // the hook are only needed while a press is being kept away from the camera.
            bool held = Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);
            if (!held)
            {
                _block = false;
                return;
            }
            if (!_block && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) && PointerOverKit())
                _block = true;
            if (!_block)
                return;

            Camera cam = Camera.main;
            if (cam != _camera)
            {
                _camera = cam;
                FindControl(cam);
            }
            if (_control == null || ReferenceEquals(_field, null))
                return;
            object current = _field.GetValue(_control);
            if (ReferenceEquals(current, _ours))
                return;
            _previous = AsFunc(current as Delegate);
            _field.SetValue(_control, _ours);
        }

        private bool NoCtrl()
        {
            if (_block)
                return true;
            if (_previous == null)
                return false;
            try
            {
                return _previous();
            }
            catch
            {
                return false;
            }
        }

        private void FindControl(Camera cam)
        {
            _control = null;
            _field = null;
            _ours = null;
            _previous = null;
            if (cam == null || ReferenceEquals(_ourMethod, null))
                return;
            foreach (MonoBehaviour mb in cam.GetComponents<MonoBehaviour>())
            {
                if (mb == null)
                    continue;
                FieldInfo f = mb.GetType().GetField(FieldName, BindingFlags.Public | BindingFlags.Instance);
                if (ReferenceEquals(f, null) || typeof(Delegate).IsAssignableFrom(f.FieldType) == false)
                    continue;
                MethodInfo invoke = f.FieldType.GetMethod("Invoke");
                if (ReferenceEquals(invoke, null) || invoke.ReturnType != typeof(bool) || invoke.GetParameters().Length != 0)
                    continue;
                try
                {
                    _ours = Delegate.CreateDelegate(f.FieldType, this, _ourMethod);
                    _control = mb;
                    _field = f;
                    return;
                }
                catch (Exception e)
                {
                    Log.Write("KitCameraGuard: could not hook " + mb.GetType().Name + ": " + e.Message);
                }
            }
        }

        // The previous condition has the game's own delegate type; turning it into a BoolFunc once avoids
        // DynamicInvoke (and its garbage) on every frame the camera asks.
        private static BoolFunc AsFunc(Delegate d)
        {
            if (d == null)
                return null;
            try
            {
                if (d.GetInvocationList().Length == 1)
                    return (BoolFunc)Delegate.CreateDelegate(typeof(BoolFunc), d.Target, d.Method);
            }
            catch
            {
            }
            return () => (bool)d.DynamicInvoke();
        }

        private static bool PointerOverKit()
        {
            Vector2 mouse = Input.mousePosition;
            return OverChild(Kit.WindowLayer, mouse) || OverChild(Kit.PopupLayer, mouse);
        }

        private static bool OverChild(RectTransform layer, Vector2 mouse)
        {
            if (layer == null)
                return false;
            for (int i = 0; i < layer.childCount; i++)
            {
                RectTransform child = layer.GetChild(i) as RectTransform;
                if (child != null && child.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(child, mouse, null))
                    return true;
            }
            return false;
        }
    }
}
