using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attached to each go-bag item. Stores its index and plays a "pop-up" showcase animation
/// (lift, grow with a bounce, slow spin) when the item is tapped.
/// </summary>
public class ItemHotspot : MonoBehaviour
{
    // =====================  EASY SETTINGS  =====================
    const float PopScale = 1.6f;     // how big the item gets (1 = no change)
    const float PopHeight = 0.30f;   // how far it lifts up
    const float SpinSpeed = 70f;     // degrees per second while showcased
    const float PopSpeed = 2.5f;     // higher = faster pop
    // ===========================================================

    public int index;

    static readonly List<ItemHotspot> All = new List<ItemHotspot>();

    Vector3 basePos;
    float pop;            // 0 = resting, 1 = fully popped
    float target;         // where pop is heading
    float spinAngle;

    // second parameter is optional so older calls like Setup(i, hint) still compile
    public void Setup(int i, Transform unused = null)
    {
        index = i;
        basePos = transform.localPosition;
        All.Add(this);
    }

    void OnDestroy() { All.Remove(this); }

    // kept so ProductController can still call it
    public void SetFound(bool found) { }

    /// <summary>Pops the item with this index and settles all the others.</summary>
    public static void PopOnly(int i)
    {
        foreach (var h in All) h.target = (h.index == i) ? 1f : 0f;
    }

    /// <summary>Settles every item back to its place.</summary>
    public static void PopNone()
    {
        foreach (var h in All) h.target = 0f;
    }

    public static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    void Update()
    {
        pop = Mathf.MoveTowards(pop, target, Time.deltaTime * PopSpeed);

        // bounce on the way up, smooth on the way down
        float e = target > 0.5f ? EaseOutBack(pop) : pop * pop * (3f - 2f * pop);

        transform.localPosition = basePos + Vector3.up * (PopHeight * e);
        transform.localScale = Vector3.one * (1f + (PopScale - 1f) * e);

        if (target > 0.5f)
        {
            spinAngle += SpinSpeed * Time.deltaTime;
            transform.localRotation = Quaternion.Euler(0f, spinAngle, 0f);
        }
        else
        {
            transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.identity, Time.deltaTime * 8f);
            if (pop <= 0f) spinAngle = 0f;
        }
    }
}