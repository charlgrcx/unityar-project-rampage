using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Watches the AR Tracked Image Manager. When the marker is found the go-bag is placed on it.
/// Also detects taps on the items. Add this to ONE empty GameObject - it adds GameManager + UIManager automatically.
/// </summary>
[RequireComponent(typeof(GameManager), typeof(UIManager))]
public class MarkerSpawner : MonoBehaviour
{
    [Tooltip("Leave empty - it is found automatically on the XR Origin.")]
    public ARTrackedImageManager imageManager;
    [Tooltip("Fallback marker size in meters if the image library has no size.")]
    public float defaultMarkerSize = 0.15f;
    [Tooltip("In the Unity Editor (no marker) spawn the bag in front of the camera for testing.")]
    public bool spawnInEditorForTesting = true;

    public ProductController Product { get; private set; }

    GameManager gm;
    UIManager ui;
    bool markerSeen;

    void Awake()
    {
        gm = GetComponent<GameManager>();
        ui = GetComponent<UIManager>();
        if (imageManager == null) imageManager = FindFirstObjectByType<ARTrackedImageManager>();
    }

    void OnEnable()  { if (imageManager != null) imageManager.trackablesChanged.AddListener(OnChanged); }
    void OnDisable() { if (imageManager != null) imageManager.trackablesChanged.RemoveListener(OnChanged); }

    void Start()
    {
#if UNITY_EDITOR
        if (spawnInEditorForTesting)
        {
            var cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                Product = ProductController.Build(0.2f);
                Product.transform.position = cam.transform.position + cam.transform.forward * 0.7f + Vector3.down * 0.2f;
                markerSeen = true;
            }
        }
#endif
        if (Product != null) Product.gameObject.SetActive(false);
    }

    void OnChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
    {
        foreach (var img in args.added) Place(img);
        foreach (var img in args.updated) Place(img);
    }

    void Place(ARTrackedImage img)
    {
        if (img.trackingState == TrackingState.None) return;
        float size = img.size.x > 0.01f ? img.size.x : defaultMarkerSize;

        if (Product == null)
        {
            Product = ProductController.Build(size);
            Product.gameObject.SetActive(false);
        }
        Product.SetMarkerSize(size);

        // follow the marker while it is actively tracked; keep last pose if it is lost
        if (img.trackingState == TrackingState.Tracking)
            Product.transform.SetPositionAndRotation(img.transform.position, img.transform.rotation);

        markerSeen = true;
    }

    void Update()
    {
        if (Product != null)
            Product.gameObject.SetActive(markerSeen && gm.CurrentStage != Stage.Start);

        // PREPARE -> INTERACT as soon as the marker is found
        if (gm.CurrentStage == Stage.Prepare && markerSeen) gm.SetStage(Stage.Interact);

        // tap an item
        if (gm.CurrentStage == Stage.Interact && Product != null && TapInput.TryGetTap(out var pos))
        {
            if (UIManager.IsOverUI(pos)) return;
            var cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                var hits = Physics.RaycastAll(cam.ScreenPointToRay(pos), 10f);
                ItemHotspot best = null;
                float bestDist = float.MaxValue;
                foreach (var h in hits)
                {
                    var hs = h.collider.GetComponentInParent<ItemHotspot>();
                    if (hs != null && h.distance < bestDist) { best = hs; bestDist = h.distance; }
                }
                if (best != null) ui.ShowItem(best.index);
                else Debug.Log("Tap hit " + hits.Length + " colliders, none were items");
            }
        }
    }
}
