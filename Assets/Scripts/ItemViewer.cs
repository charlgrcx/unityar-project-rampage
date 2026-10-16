using UnityEngine;

/// <summary>
/// Shows ONE essential in AR at a time. While "placing", the item hovers where the middle of the screen
/// points on the table; tap to put it down. After that the player taps the item to earn its points and
/// read about it, and can turn and zoom it.
/// The table is the flat surface the marker lies on, so no plane detection is needed.
/// Add this component to the same GameObject as MarkerSpawner.
/// </summary>
public class ItemViewer : MonoBehaviour
{
    // =====================  EASY SETTINGS  =====================
    const float ZoomMin = 0.5f, ZoomMax = 2.5f;
    const float MaxReach = 3f;     // meters: how far away the item can be put down
    const float FrontYaw = 0f;     // if an item shows its back to the player, change this to 180
    // ===========================================================

    public int Index { get; private set; } = -1;
    public bool Placing { get; private set; }
    public bool HasItem { get { return root != null; } }

    /// <summary>Pulse gently to say "tap me" (set by MarkerSpawner until the item has been tapped).</summary>
    public bool Invite;

    Transform root, spin, surface;
    float markerSize = 0.15f, zoom = 1f, spinAngle;
    Vector3 lastPos;

    static Camera Cam() { return Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>(); }

    /// <summary>Shows item 'index' on the surface of 'surfaceRef' (the bag / marker), ready to be placed.</summary>
    public void Show(int index, Transform surfaceRef, float marker)
    {
        Clear();
        Index = index; surface = surfaceRef; markerSize = marker;
        zoom = 1f; spinAngle = 0f;

        var holder = new GameObject("ShownItem");
        var item = ItemLibrary.CreateItem(index);
        item.transform.SetParent(holder.transform, false);
        root = holder.transform;
        spin = item.transform;

        // a collider a bit bigger than the item, so a tap on it is easy to hit
        var b = ItemLibrary.BoundsOf(item);
        var col = item.AddComponent<BoxCollider>();
        col.center = b.center; col.size = b.size * 1.3f;

        lastPos = surface.position + surface.right * markerSize * 1.2f;   // start beside the bag
        root.position = lastPos;
        root.localScale = Vector3.zero;                                    // grows in
        Placing = true;
    }

    /// <summary>Puts the item down where it is hovering.</summary>
    public void Place() { if (root != null) Placing = false; }

    public void Rotate(float degrees) { spinAngle += degrees; }
    public void Zoom(float factor) { zoom = Mathf.Clamp(zoom * factor, ZoomMin, ZoomMax); }
    public void SetMarkerSize(float s) { markerSize = s; }

    /// <summary>True if this collider belongs to the item that is being shown.</summary>
    public bool OwnsCollider(Collider c) { return root != null && c != null && c.transform.IsChildOf(root); }

    public void Clear()
    {
        if (root != null) Destroy(root.gameObject);
        root = spin = null;
        Index = -1; Placing = false;
    }

    void Update()
    {
        if (root == null) return;

        if (Placing)
        {
            var cam = Cam();
            if (cam != null && surface != null)
            {
                var plane = new Plane(surface.up, surface.position);
                var ray = new Ray(cam.transform.position, cam.transform.forward);
                if (plane.Raycast(ray, out float d) && d > 0f && d < MaxReach) lastPos = ray.GetPoint(d);
                root.position = Vector3.Lerp(root.position, lastPos, Time.deltaTime * 12f);

                // turn the item so its front faces the player
                var toCam = Vector3.ProjectOnPlane(cam.transform.position - root.position, surface.up);
                if (toCam.sqrMagnitude > 0.0001f)
                    root.rotation = Quaternion.LookRotation(-toCam.normalized, surface.up) * Quaternion.Euler(0f, FrontYaw, 0f);
            }
        }

        root.localScale = Vector3.Lerp(root.localScale, Vector3.one * markerSize * zoom, Time.deltaTime * 8f);
        spin.localRotation = Quaternion.Slerp(spin.localRotation, Quaternion.Euler(0f, spinAngle, 0f), Time.deltaTime * 8f);

        // pulse until the player has tapped the item for its points
        float pulse = (Invite && !Placing) ? 1f + 0.06f * Mathf.Sin(Time.time * 4f) : 1f;
        spin.localScale = Vector3.Lerp(spin.localScale, Vector3.one * pulse, Time.deltaTime * 10f);
    }
}
