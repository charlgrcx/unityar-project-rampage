using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the 3D go-bag + 6 essentials.
/// Uses your prefabs in Assets/Resources/GoBag (Bag, Item_0 ... Item_5) when they exist,
/// otherwise simple shapes. Imported models are AUTOMATICALLY resized, centered and
/// placed on the ground, so you only change the easy settings below.
/// Model units: 1 unit = the width of the printed marker.
/// </summary>
public class ProductController : MonoBehaviour
{
    // =====================================================================
    //  EASY SETTINGS  (change these numbers, save, press Play)
    // =====================================================================

    // How far the 6 items sit from the bag (bigger = more spread out).
    const float RingRadius = 0.70f;

    // Size of the bag = its largest dimension, in marker widths (0.7 = 70% of the marker).
    const float BagSize = 0.7f;

    const float BagYOffset = 0.5f;   // + moves up, - moves down

    // If the bag is lying down, try (-90, 0, 0). If that is upside down, try (90, 0, 0).
    static readonly Vector3 BagRotation = new Vector3(90f, 0, 0f);

    // Size of each item (largest dimension). Order: Item_0 ... Item_5
    // Item_0 water bottle, Item_1 first-aid kit, Item_2 flashlight,
    // Item_3 emergency radio, Item_4 whistle, Item_5 document pouch
    static readonly float[] ItemSize = { 0.35f, 0.35f, 0.35f, 0.35f, 0.30f, 0.35f };

    // Extra rotation per item (same trick as the bag: (-90,0,0) or (90,0,0) if one is lying down).
    static readonly Vector3[] ItemRotation =
{
    new Vector3(0f, 180f, 0f),    // Item_0 clothes (back to normal)
    new Vector3(0f, 180f, 0f),   // Item_1 first-aid kit (keep as it was)
    new Vector3(-90f, 180f, 0f), // Item_2 face mask box (turns the brand to the front)
    new Vector3(90f, 0f, 0f),   // Item_3 water bottle (stands it up, cap on top)
    Vector3.zero,                // Item_4 whistle
    Vector3.zero                 // Item_5 flashlight
};

    // Move an item up (+) or down (-) if it floats or sinks.
    static readonly float[] ItemYOffset = { 0f, 0f, 0f, 0f, 0f, 0f };

    // Colors for the Color button. The first one ("Original") keeps the bag's own colors.
    static readonly Color[] BagColors =
    {
        Color.white,
        new Color(1f, 0.48f, 0.1f),
        new Color(0.1f, 0.25f, 0.5f),
        new Color(0.35f, 0.42f, 0.15f),
        new Color(0.945f, 0.655f, 0.690f)
    };
    static readonly string[] BagColorNames = { "Original", "Orange", "Navy", "Olive", "Pink" };

    // =====================================================================

    static readonly Color FallbackBagColor = new Color(1f, 0.48f, 0.1f);

    Transform spin;
    readonly List<Renderer> bagMain = new List<Renderer>();   // fallback shapes only
    readonly List<Renderer> bagDark = new List<Renderer>();   // fallback shapes only
    readonly List<Material> bagMats = new List<Material>();   // imported bag materials
    readonly List<Color> bagMatOriginal = new List<Color>();
    readonly List<ItemHotspot> hotspots = new List<ItemHotspot>();
    int colorIndex;
    float spinAngle, zoom = 1f, markerSize = 0.15f;

    // ---------- public API ----------
    public static ProductController Build(float size)
    {
        var go = new GameObject("GoBagProduct");
        var pc = go.AddComponent<ProductController>();
        pc.spin = new GameObject("Spin").transform;
        pc.spin.SetParent(go.transform, false);
        pc.BuildBag();
        pc.BuildItems();
        pc.SetBagColor(0);
        pc.markerSize = size;
        go.transform.localScale = Vector3.one * size;
        return pc;
    }

    public void SetMarkerSize(float s) { markerSize = s; }
    public void Rotate(float degrees) { spinAngle += degrees; }
    public void Zoom(float factor) { zoom = Mathf.Clamp(zoom * factor, 0.5f, 2.2f); }
    public string CycleColor() { SetBagColor((colorIndex + 1) % BagColors.Length); return BagColorNames[colorIndex]; }

    // ---------- unity ----------
    void Start()
    {
        if (GameManager.Instance != null) GameManager.Instance.Changed += RefreshHints;
        RefreshHints();
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.Changed -= RefreshHints;
    }

    void Update()
    {
        spin.localRotation = Quaternion.Slerp(spin.localRotation, Quaternion.Euler(0, spinAngle, 0), Time.deltaTime * 8f);
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * markerSize * zoom, Time.deltaTime * 8f);
    }

    void RefreshHints()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        foreach (var h in hotspots) h.SetFound(gm.IsFound(h.index));
    }

    // ---------- colour ----------
    void SetBagColor(int i)
    {
        colorIndex = i;
        var c = BagColors[i];

        // imported bag: "Original" restores its own colors, others blend toward the chosen color
        for (int k = 0; k < bagMats.Count; k++)
            SetMatColor(bagMats[k], i == 0 ? bagMatOriginal[k] : Color.Lerp(bagMatOriginal[k], c, 0.8f));

        // fallback bag made of shapes
        var fc = i == 0 ? FallbackBagColor : c;
        foreach (var r in bagMain) r.material.color = fc;
        foreach (var r in bagDark) r.material.color = fc * 0.65f + new Color(0, 0, 0, 0.35f);
    }

    static Color GetMatColor(Material m)
    {
        if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
        if (m.HasProperty("_Color")) return m.GetColor("_Color");
        return Color.white;
    }

    static void SetMatColor(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    // ---------- helpers ----------
    static T Pick<T>(T[] arr, int i, T fallback) { return (arr != null && i < arr.Length) ? arr[i] : fallback; }

    static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Color color, Vector3 euler = default)
    {
        var g = GameObject.CreatePrimitive(t);
        Destroy(g.GetComponent<Collider>());            // taps use one collider per item instead
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(euler);
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().material.color = color;
        return g;
    }

    /// <summary>Bounding box of all renderers of 'go', measured in the local space of 'space'.</summary>
    static Bounds LocalBounds(Transform space, GameObject go)
    {
        bool any = false;
        var b = new Bounds(Vector3.zero, Vector3.zero);
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var wb = r.bounds;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        var corner = space.InverseTransformPoint(wb.center + Vector3.Scale(wb.extents, new Vector3(sx, sy, sz)));
                        if (!any) { b = new Bounds(corner, Vector3.zero); any = true; }
                        else b.Encapsulate(corner);
                    }
        }
        return b;
    }

    /// <summary>
    /// Puts an imported prefab into the scene: removes stray cameras/lights/colliders,
    /// resizes it so its largest side = targetSize, centers it horizontally and sits it on the ground.
    /// Returns the final height of the model.
    /// </summary>
    static float PlaceModel(GameObject prefab, Transform parent, float targetSize, Vector3 extraEuler, float yOffset, out GameObject model)
    {
        // wrapper = unrotated space used for measuring and positioning
        var wrapper = new GameObject(prefab.name + "_Fit").transform;
        wrapper.SetParent(parent, false);
        wrapper.localPosition = Vector3.zero;
        wrapper.localRotation = Quaternion.identity;

        // rot = holds the extra rotation, sits inside the wrapper
        var rot = new GameObject(prefab.name + "_Rot").transform;
        rot.SetParent(wrapper, false);
        rot.localRotation = Quaternion.Euler(extraEuler);

        model = Instantiate(prefab, rot);
        model.name = prefab.name;

        // clean out things that should not come with a model
        foreach (var c in model.GetComponentsInChildren<Camera>(true)) Destroy(c);
        foreach (var l in model.GetComponentsInChildren<Light>(true)) Destroy(l);
        foreach (var a in model.GetComponentsInChildren<AudioListener>(true)) Destroy(a);
        foreach (var col in model.GetComponentsInChildren<Collider>(true)) Destroy(col);

        var b = LocalBounds(wrapper, model);
        float largest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (largest < 0.0001f) return 0.3f;

        // 1) resize
        rot.localScale *= targetSize / largest;
        b = LocalBounds(wrapper, model);

        // 2) center left/right + front/back, bottom on the ground (now in true upright space)
        rot.localPosition += new Vector3(-b.center.x, -b.min.y + yOffset, -b.center.z);

        return b.size.y + yOffset;
    }

    // ---------- model building ----------
    void BuildBag()
    {
        var bagPrefab = Resources.Load<GameObject>("GoBag/Bag");
        if (bagPrefab != null)
        {
            GameObject model;
            PlaceModel(bagPrefab, spin, BagSize, BagRotation, BagYOffset, out model);

            // remember the bag's own colors so the Color button can change / restore them
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                foreach (var mat in r.materials)
                {
                    bagMats.Add(mat);
                    bagMatOriginal.Add(GetMatColor(mat));
                }
            return;
        }

        // ---- fallback: simple bag from primitives ----
        var bag = new GameObject("Bag").transform;
        bag.SetParent(spin, false);
        var grey = new Color(0.15f, 0.15f, 0.15f);

        bagMain.Add(Prim(PrimitiveType.Cube, bag, new Vector3(0, 0.2f, 0), new Vector3(0.55f, 0.4f, 0.3f), Color.white).GetComponent<Renderer>());
        bagMain.Add(Prim(PrimitiveType.Cylinder, bag, new Vector3(0, 0.4f, 0), new Vector3(0.3f, 0.275f, 0.3f), Color.white, new Vector3(0, 0, 90)).GetComponent<Renderer>());
        bagDark.Add(Prim(PrimitiveType.Cube, bag, new Vector3(0, 0.14f, 0.17f), new Vector3(0.4f, 0.22f, 0.08f), Color.white).GetComponent<Renderer>());

        // handle + straps
        Prim(PrimitiveType.Cube, bag, new Vector3(0, 0.62f, 0), new Vector3(0.22f, 0.03f, 0.04f), grey);
        Prim(PrimitiveType.Cube, bag, new Vector3(-0.1f, 0.585f, 0), new Vector3(0.03f, 0.07f, 0.04f), grey);
        Prim(PrimitiveType.Cube, bag, new Vector3(0.1f, 0.585f, 0), new Vector3(0.03f, 0.07f, 0.04f), grey);
        Prim(PrimitiveType.Cube, bag, new Vector3(-0.2f, 0.22f, 0.215f), new Vector3(0.04f, 0.3f, 0.01f), grey);
        Prim(PrimitiveType.Cube, bag, new Vector3(0.2f, 0.22f, 0.215f), new Vector3(0.04f, 0.3f, 0.01f), grey);
    }

    void BuildItems()
    {
        for (int i = 0; i < GoBagData.Items.Length; i++)
        {
            float a = i * Mathf.PI * 2f / GoBagData.Items.Length;
            var root = new GameObject("Item_" + GoBagData.Items[i].name).transform;
            root.SetParent(spin, false);
            root.localPosition = new Vector3(Mathf.Cos(a) * RingRadius, 0, Mathf.Sin(a) * RingRadius);

            float top = BuildItemShape(i, root);

            // one tap collider that covers the whole item
            var col = root.gameObject.AddComponent<SphereCollider>();
            col.center = new Vector3(0, top * 0.5f, 0);
            col.radius = Mathf.Max(0.25f, top * 0.6f);

            var hs = root.gameObject.AddComponent<ItemHotspot>();
            hs.Setup(i);
            hotspots.Add(hs);
        }
    }

    /// <summary>Builds one item and returns its height.</summary>
    float BuildItemShape(int i, Transform p)
    {
        var itemPrefab = Resources.Load<GameObject>("GoBag/Item_" + i);
        if (itemPrefab != null)
        {
            GameObject model;
            return PlaceModel(itemPrefab, p, Pick(ItemSize, i, 0.35f), Pick(ItemRotation, i, Vector3.zero), Pick(ItemYOffset, i, 0f), out model);
        }

        // ---- fallback: simple shapes ----
        var grey = new Color(0.2f, 0.2f, 0.22f);
        switch (i)
        {
            case 0: // water bottle
                Prim(PrimitiveType.Cylinder, p, new Vector3(0, 0.14f, 0), new Vector3(0.14f, 0.14f, 0.14f), new Color(0.17f, 0.55f, 1f));
                Prim(PrimitiveType.Cylinder, p, new Vector3(0, 0.305f, 0), new Vector3(0.08f, 0.025f, 0.08f), Color.white);
                break;
            case 1: // first-aid kit
                Prim(PrimitiveType.Cube, p, new Vector3(0, 0.09f, 0), new Vector3(0.26f, 0.18f, 0.1f), Color.white);
                Prim(PrimitiveType.Cube, p, new Vector3(0, 0.09f, 0), new Vector3(0.16f, 0.04f, 0.105f), new Color(0.9f, 0.2f, 0.2f));
                Prim(PrimitiveType.Cube, p, new Vector3(0, 0.09f, 0), new Vector3(0.04f, 0.14f, 0.105f), new Color(0.9f, 0.2f, 0.2f));
                break;
            case 2: // flashlight
                Prim(PrimitiveType.Cylinder, p, new Vector3(0, 0.13f, 0), new Vector3(0.07f, 0.13f, 0.07f), grey);
                Prim(PrimitiveType.Cylinder, p, new Vector3(0, 0.275f, 0), new Vector3(0.12f, 0.03f, 0.12f), new Color(1f, 0.82f, 0.2f));
                Prim(PrimitiveType.Cylinder, p, new Vector3(0, 0.306f, 0), new Vector3(0.1f, 0.002f, 0.1f), Color.white);
                break;
            case 3: // emergency radio
                Prim(PrimitiveType.Cube, p, new Vector3(0, 0.08f, 0), new Vector3(0.26f, 0.16f, 0.08f), new Color(1f, 0.5f, 0.1f));
                Prim(PrimitiveType.Cylinder, p, new Vector3(-0.05f, 0.08f, 0.042f), new Vector3(0.1f, 0.004f, 0.1f), grey, new Vector3(90, 0, 0));
                Prim(PrimitiveType.Cylinder, p, new Vector3(0.1f, 0.26f, 0), new Vector3(0.01f, 0.1f, 0.01f), Color.gray, new Vector3(0, 0, -12));
                Prim(PrimitiveType.Sphere, p, new Vector3(0.07f, 0.1f, 0.045f), Vector3.one * 0.04f, Color.white);
                break;
            case 4: // whistle
                Prim(PrimitiveType.Cylinder, p, new Vector3(0, 0.045f, 0), new Vector3(0.08f, 0.07f, 0.08f), new Color(1f, 0.35f, 0.2f), new Vector3(0, 0, 90));
                Prim(PrimitiveType.Cube, p, new Vector3(0.1f, 0.05f, 0), new Vector3(0.09f, 0.04f, 0.06f), new Color(1f, 0.35f, 0.2f));
                Prim(PrimitiveType.Sphere, p, new Vector3(-0.1f, 0.045f, 0), Vector3.one * 0.04f, Color.gray);
                break;
            default: // document pouch
                Prim(PrimitiveType.Cube, p, new Vector3(0, 0.015f, 0), new Vector3(0.28f, 0.03f, 0.2f), new Color(0.55f, 0.8f, 0.98f));
                Prim(PrimitiveType.Cube, p, new Vector3(0, 0.017f, 0), new Vector3(0.2f, 0.032f, 0.14f), Color.white);
                break;
        }
        return 0.35f;
    }
}