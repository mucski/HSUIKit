namespace HSUIKit
{
    // Plain non-generic delegates instead of Action<T>/Func<T>: old Mono fails
    // Assembly.GetTypes() on closed BCL generics over Unity value types (e.g. Func<Color>),
    // so the whole kit avoids them.
    public delegate void VoidHandler();
    public delegate void BoolHandler(bool value);
    public delegate void IntHandler(int value);
    public delegate void FloatHandler(float value);
    public delegate void StringHandler(string value);

    // Receives the EASED progress (0..1, may overshoot for Back/Elastic eases).
    public delegate void TweenSetter(float t);

    public delegate void ColorHandler(UnityEngine.Color value);
    public delegate void Vector3Handler(UnityEngine.Vector3 value);
    public delegate void HotkeyHandler(KitHotkey value);
    public delegate void PointHandler(float x, float y);   // normalized 0..1 within a rect
    public delegate KitMenu MenuBuilder();

    // Pixel-center coordinates in, color out - for colored procedural textures (hue bars).
    public delegate UnityEngine.Color PixelFunc(float x, float y);

    // Pixel-center coordinates in, alpha (0..1) out - used by the procedural sprite builder.
    public delegate float AlphaFunc(float x, float y);
}
