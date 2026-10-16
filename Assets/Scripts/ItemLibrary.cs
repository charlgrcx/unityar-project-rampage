using UnityEngine;

/// <summary>
/// Loads your 3D models from Assets/Resources/GoBag (Bag, Item_0 ... Item_5), fits each one to a size
/// (1 unit = the width of the printed marker) and makes the picture thumbnails used on the item buttons.
/// The thumbnails are photos of the real 3D models, so no image files are needed.
/// </summary>
public static class ItemLibrary
{
    // =====================  EASY SETTINGS  =====================
    public const float BagSize = 0.9f;     // largest side of the bag on the marker, in marker widths
    public const float ItemSize = 1.1f;    // largest side of an item when it is shown in AR, in marker widths
    const int ThumbSize = 256;             // thumbnail size in pixels
    const int PreviewLayer = 31;           // an unused layer, only used for a moment while taking thumbnails

    // Same rotations you already tuned in ProductController.
    static readonly Vector3 BagRotation = new Vector3(90f, 0f, 0f);
    static readonly Vector3[] ItemRotation =
    {
        new Vector3(0f, 180f, 0f),     // Item_0 clothes
        new Vector3(0f, 180f, 0f),     // Item_1 first-aid kit
        new Vector3(-90f, 180f, 0f),   // Item_2 face mask
        new Vector3(90f, 0f, 0f),      // Item_3 water bottle
        Vector3.zero,                  // Item_4 whistle
        Vector3.zero                   // Item_5 flashlight
    };
    // ===========================================================

    static readonly Sprite[] thumbs = new Sprite[GoBagData.Items.Length];

    public static GameObject CreateBag()
    {
        return Build("GoBag/Bag", BagRotation, BagSize, PrimitiveType.Cube, new Color(1f, 0.48f, 0.1f), "Bag");
    }

    public static GameObject CreateItem(int i, float size = ItemSize)
    {
        var rot = i < ItemRotation.Length ? ItemRotation[i] : Vector3.zero;
        return Build("GoBag/Item_" + i, rot, size, PrimitiveType.Sphere, new Color(0.9f, 0.47f, 0.34f), "Item_" + i);
    }

    /// <summary>Makes the thumbnails for all items right now (call once at startup so the first menu opens instantly).</summary>
    public static void PrepareThumbnails()
    {
        for (int i = 0; i < thumbs.Length; i++) Thumbnail(i);
    }

    /// <summary>A transparent picture of the item's 3D model, for buttons.</summary>
    public static Sprite Thumbnail(int i)
    {
        if (thumbs[i] != null) return thumbs[i];

        var model = CreateItem(i, 1f);
        model.transform.position = new Vector3(0f, -5000f, 0f);   // far away from everything else
        SetLayer(model, PreviewLayer);

        var camGo = new GameObject("ThumbnailCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.enabled = false;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask = 1 << PreviewLayer;
        cam.orthographic = true;
        cam.orthographicSize = 0.7f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 10f;

        // look from the front (the side a player sees from the bottom edge of the marker), a little from above
        var center = BoundsOf(model).center;
        camGo.transform.position = center + new Vector3(0.7f, 0.55f, -2f);
        camGo.transform.LookAt(center);

        var rt = RenderTexture.GetTemporary(ThumbSize, ThumbSize, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(ThumbSize, ThumbSize, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, ThumbSize, ThumbSize), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        Object.Destroy(camGo);
        Object.Destroy(model);

        thumbs[i] = Sprite.Create(tex, new Rect(0, 0, ThumbSize, ThumbSize), new Vector2(0.5f, 0.5f));
        return thumbs[i];
    }

    /// <summary>Bounding box of everything visible inside 'go' (world space).</summary>
    public static Bounds BoundsOf(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    // ---------- helpers ----------
    // Returns a root object at the origin whose child model is rotated, resized to 'size' (largest side),
    // centered left-right/front-back and standing on y = 0.
    static GameObject Build(string path, Vector3 euler, float size, PrimitiveType fallback, Color fallbackColor, string name)
    {
        var root = new GameObject(name);
        var prefab = Resources.Load<GameObject>(path);
        GameObject model;
        if (prefab != null)
        {
            model = Object.Instantiate(prefab);
        }
        else
        {
            Debug.LogWarning("ItemLibrary: '" + path + "' not found in Assets/Resources - using a simple shape instead.");
            model = GameObject.CreatePrimitive(fallback);
            model.GetComponent<Renderer>().material.color = fallbackColor;
        }
        foreach (var c in model.GetComponentsInChildren<Collider>()) Object.Destroy(c);   // taps use our own collider
        model.SetActive(true);
        model.transform.SetParent(root.transform, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(euler);

        var baseScale = model.transform.localScale;
        var b = BoundsOf(model);
        float largest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (largest > 0.0001f) model.transform.localScale = baseScale * (size / largest);

        b = BoundsOf(model);
        model.transform.localPosition += new Vector3(-b.center.x, -b.min.y, -b.center.z);
        return root;
    }

    static void SetLayer(GameObject go, int layer)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.layer = layer;
    }
}
