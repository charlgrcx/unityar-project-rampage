using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Tiny helper so taps work with both the new Input System and the old Input Manager.</summary>
public static class TapInput
{
    public static bool TryGetTap(out Vector2 pos)
    {
        pos = default;
#if ENABLE_INPUT_SYSTEM
        var ts = Touchscreen.current;
        if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) { pos = ts.primaryTouch.position.ReadValue(); return true; }
        var m = Mouse.current;
        if (m != null && m.leftButton.wasPressedThisFrame) { pos = m.position.ReadValue(); return true; }
        return false;
#else
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) { pos = Input.GetTouch(0).position; return true; }
        if (Input.GetMouseButtonDown(0)) { pos = Input.mousePosition; return true; }
        return false;
#endif
    }
}
