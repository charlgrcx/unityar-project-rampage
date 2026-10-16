using UnityEngine;

/// <summary>
/// The go-bag that appears on the marker. It wiggles gently to say "tap me", and has a collider
/// (a bit bigger than the bag, easy for small fingers) so MarkerSpawner can detect the tap.
/// Model units: 1 unit = the width of the printed marker.
/// </summary>
public class BagController : MonoBehaviour
{
    /// <summary>Wiggle to invite a tap.</summary>
    public bool Invite = true;

    Transform visual;
    float markerSize = 0.15f;

    public static BagController Create(float markerSize)
    {
        var go = new GameObject("GoBagOnMarker");
        var bc = go.AddComponent<BagController>();

        var bag = ItemLibrary.CreateBag();
        bag.transform.SetParent(go.transform, false);
        bc.visual = bag.transform;

        var b = ItemLibrary.BoundsOf(bag);
        var col = go.AddComponent<BoxCollider>();
        col.center = b.center;
        col.size = b.size * 1.2f;

        bc.markerSize = markerSize;
        go.transform.localScale = Vector3.one * markerSize;
        return bc;
    }

    public void SetMarkerSize(float s) { markerSize = s; }

    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * markerSize, Time.deltaTime * 8f);

        float t = Time.time;
        float pulse = Invite ? 1f + 0.05f * Mathf.Sin(t * 4f) : 1f;
        visual.localScale = Vector3.Lerp(visual.localScale, Vector3.one * pulse, Time.deltaTime * 10f);
        visual.localRotation = Quaternion.Euler(0f, Invite ? 6f * Mathf.Sin(t * 2.5f) : 0f, 0f);
    }
}
