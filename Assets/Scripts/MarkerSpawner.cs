using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Watches the AR Tracked Image Manager. When the marker is found the go-bag appears on it.
/// Tapping the bag opens the item chooser; tapping while an item hovers puts it down.
/// Add this to ONE empty GameObject - it adds GameManager, UIManager and ItemViewer automatically.
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

    public BagController Bag { get; private set; }
    public ItemViewer Viewer { get; private set; }
    public Transform BagTransform { get { return Bag != null ? Bag.transform : null; } }
    public float MarkerSize { get; private set; }

    GameManager gm;
    UIManager ui;
    bool markerSeen;

    void Awake()
    {
        gm = GetComponent<GameManager>();
        ui = GetComponent<UIManager>();
        Viewer = GetComponent<ItemViewer>();
        if (Viewer == null) Viewer = gameObject.AddComponent<ItemViewer>();
        MarkerSize = defaultMarkerSize;
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
                MarkerSize = 0.2f;
                Bag = BagController.Create(MarkerSize);
                Bag.transform.position = cam.transform.position + cam.transform.forward * 0.7f + Vector3.down * 0.25f;
                Viewer.SetMarkerSize(MarkerSize);
                markerSeen = true;
            }
        }
#endif
        if (Bag != null) Bag.gameObject.SetActive(false);
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
        MarkerSize = size;

        if (Bag == null)
        {
            Bag = BagController.Create(size);
            Bag.gameObject.SetActive(false);
        }
        Bag.SetMarkerSize(size);
        Viewer.SetMarkerSize(size);

        // follow the marker while it is actively tracked; keep the last pose if it is lost
        if (img.trackingState == TrackingState.Tracking)
            Bag.transform.SetPositionAndRotation(img.transform.position, img.transform.rotation);

        markerSeen = true;
    }

    void Update()
    {
        if (Bag != null)
        {
            Bag.gameObject.SetActive(markerSeen && gm.CurrentStage != Stage.Start && !Viewer.HasItem); Bag.Invite = !ui.ChooserOpen && !Viewer.HasItem;     // wiggle only when there is nothing else to do
            Viewer.Invite = Viewer.HasItem && !gm.IsFound(Viewer.Index);   // the item pulses until it has been tapped
        }

        // PREPARE -> INTERACT as soon as the marker is found
        if (gm.CurrentStage == Stage.Prepare && markerSeen) gm.SetStage(Stage.Interact);

        if (gm.CurrentStage != Stage.Interact || Bag == null) return;
        if (!TapInput.TryGetTap(out var pos)) return;
        if (UIManager.IsOverUI(pos)) return;

        // 1) an item is hovering in AR: this tap puts it down
        if (Viewer.Placing) { Viewer.Place(); return; }

        if (ui.ChooserOpen) return;
        var cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        if (cam == null) return;

        // 2) a tap on the placed item gives points and shows its info
        // 3) a tap on the bag opens the item chooser
        bool onBag = false;
        foreach (var h in Physics.RaycastAll(cam.ScreenPointToRay(pos), 10f))
        {
            if (Viewer.OwnsCollider(h.collider)) { ui.OnItemTapped(Viewer.Index); return; } 
            if (h.collider.GetComponentInParent<BagController>() != null) onBag = true;
        }
        if (onBag) ui.OpenChooser();
    }
}
