using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Builds the whole UI from code (no prefabs / no manual wiring) and runs the flow:
/// Start -> Prepare -> Interact -> Quiz -> Complete.
///
/// RESTYLED VERSION: TextMeshPro, Lilita One + Manrope fonts, rounded cards,
/// soft shadows, screen fades, safe-area support and kid-friendly hints.
///
/// FONTS: put LilitaOne-Regular.ttf and Manrope-Bold.ttf (static file, not the variable one)
/// inside Assets/Resources/Fonts/. No other setup is needed.
/// </summary>
public class UIManager : MonoBehaviour
{
    // =====================================================================
    //  EASY SETTINGS  (colors, corner roundness)
    // =====================================================================
    static readonly Color Olive = Hex("#617144");      // main green
    static readonly Color Coral = Hex("#DF7857");      // main action color
    static readonly Color Peach = Hex("#E8AB99");      // soft accent
    static readonly Color Cream = Hex("#EFE8D5");      // backgrounds and cards
    static readonly Color Ink = Hex("#2B3320");        // dark text (very readable on cream, peach and coral)
    static readonly Color Berry = Hex("#B5452B");      // wrong answers
    static readonly Color SlotEmpty = Hex("#D9CFB3");  // empty "found" slot

    const float Corner = 0.8f;   // smaller number = rounder corners (0.5 = very round, 1 = less round)
    // =====================================================================

    GameManager gm;
    MarkerSpawner spawner;
    TMP_FontAsset headFont, bodyFont;
    Sprite roundSprite, outlineSprite, circleSprite, ringSprite;
    Transform root;
    RectTransform overlay;

    GameObject startPanel, preparePanel, hudPanel, infoCard, quizPanel, resultPanel, toastBox, hintBox, feedbackBox;
    TextMeshProUGUI progressText, pointsText, toastText, hintText, infoTitle, infoBody, infoTip, infoPts,
        quizHeader, quizPoints, quizQuestion, quizFeedback, nextLabel, floatText, chooserBottomLabel, resultScore, resultOutOf, resultRank;
    Image infoPtsBox, scanFrame;
    Image[] slots;
    RectTransform cardRect, floatRect;
    CanvasGroup floatGroup;
    Button nextButton;
    Button[] optionButtons = new Button[4];
    TextMeshProUGUI[] optionTexts = new TextMeshProUGUI[4];

    readonly List<CanvasGroup> fades = new List<CanvasGroup>();
    readonly List<RectTransform> safeRects = new List<RectTransform>();
    Rect lastSafe;
    Vector2 cardMin, cardMax;
    int qIndex; bool answered;
    float toastTimer, cardT = 1f, floatT = 1f;

    // item chooser (the "open the bag" panel) and the bar with Turn / Zoom
    static readonly Color FoundTint = Color.Lerp(Olive, Cream, 0.7f);
    RectTransform chooserRect;
    GameObject viewBar;
    Image[] chooserBg;
    TextMeshProUGUI[] chooserName;
    GameObject[] chooserFound;
    Vector2 chooserMin, chooserMax;
    bool chooserOpen;
    float chooserT = 1f;
    public bool ChooserOpen { get { return chooserOpen; } }

    // summary screen
    static readonly string[] BadgeTitles = { "First Find", "Explorer", "Quiz Ace", "Go-Bag Master" };
    static readonly string[] BadgeDescs = { "Found your first item", "Found every item", "Perfect quiz score", "90% or more" };
    Image ringFill;
    float ringShown;
    TextMeshProUGUI[] statValue, badgeText;
    Image[] badgeBg;

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    static Color A(Color c, float a) { return new Color(c.r, c.g, c.b, a); }
    static string HexOf(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c); }

    // ================= setup =================
    void Start()
    {
        gm = GameManager.Instance;
        spawner = GetComponent<MarkerSpawner>();

        headFont = FindFont("lilita");
        bodyFont = FindFont("manrope");
        if (headFont == null) Debug.LogWarning("UIManager: Lilita One not found in Assets/Resources/Fonts - using the default font.");
        if (bodyFont == null) Debug.LogWarning("UIManager: Manrope not found in Assets/Resources/Fonts - using the default font.");
        if (headFont == null) headFont = bodyFont;
        if (bodyFont == null) bodyFont = headFont;

        roundSprite = MakeSprite(128, 48f, 0f, true);
        outlineSprite = MakeSprite(128, 48f, 8f, true);
        circleSprite = MakeSprite(128, 64f, 0f, false);
        ringSprite = MakeSprite(128, 64f, 16f, false);

        ItemLibrary.PrepareThumbnails();     // pictures of the 3D models for the item buttons
        EnsureEventSystem();
        BuildCanvas();
        gm.Changed += Refresh;
        Refresh();
    }

    void OnDestroy() { if (gm != null) gm.Changed -= Refresh; }

    /// <summary>Finds a font in Resources/Fonts by name (a ready TMP font asset, or a .ttf that is converted at runtime).</summary>
    static TMP_FontAsset FindFont(string key)
    {
        foreach (var a in Resources.LoadAll<TMP_FontAsset>("Fonts"))
            if (a.name.ToLower().Contains(key)) return a;
        foreach (var f in Resources.LoadAll<Font>("Fonts"))
            if (f.name.ToLower().Contains(key)) return TMP_FontAsset.CreateFontAsset(f);
        return null;
    }

    /// <summary>Makes a rounded-rectangle (or ring, or circle) sprite in code, so no image files are needed.</summary>
    static Sprite MakeSprite(int size, float radius, float thickness, bool sliced)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[size * size];
        float half = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                float d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;   // < 0 inside
                float a = Mathf.Clamp01(0.5f - d);
                if (thickness > 0f) a = Mathf.Min(a, Mathf.Clamp01(d + thickness + 0.5f));
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        var border = sliced ? new Vector4(radius, radius, radius, radius) : Vector4.zero;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }

    public static bool IsOverUI(Vector2 pos)
    {
        if (EventSystem.current == null) return false;
        var data = new PointerEventData(EventSystem.current) { position = pos };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, results);
        return results.Count > 0;
    }

    // ================= UI helpers =================
    RectTransform R(string n, Transform p, float x0, float y0, float x1, float y1)
    {
        var rt = new GameObject(n, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(p, false);
        rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    /// <summary>Plain rectangle (use for full-screen backgrounds).</summary>
    Image Fill(Transform p, Color c, float x0, float y0, float x1, float y1, bool raycast = true)
    {
        var img = R("Fill", p, x0, y0, x1, y1).gameObject.AddComponent<Image>();
        img.color = c; img.raycastTarget = raycast;
        return img;
    }

    /// <summary>Rounded card.</summary>
    Image Card(Transform p, Color c, float x0, float y0, float x1, float y1, bool raycast = true)
    {
        var img = Fill(p, c, x0, y0, x1, y1, raycast);
        img.sprite = roundSprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = Corner;
        return img;
    }

    /// <summary>Circle of a fixed size, anchored to a point (ax, ay) with an optional pixel offset.</summary>
    Image Blob(Transform p, Color c, float ax, float ay, float size, float ox = 0f, float oy = 0f)
    {
        var rt = R("Blob", p, ax, ay, ax, ay);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = new Vector2(ox, oy);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = circleSprite; img.color = c; img.raycastTarget = false;
        return img;
    }

    TextMeshProUGUI Txt(Transform p, string s, float size, TextAlignmentOptions a, Color c,
                        float x0, float y0, float x1, float y1, bool head = false)
    {
        var t = R("Text", p, x0, y0, x1, y1).gameObject.AddComponent<TextMeshProUGUI>();
        var f = head ? headFont : bodyFont;
        if (f != null) t.font = f;
        t.text = s; t.fontSize = size; t.alignment = a; t.color = c; t.raycastTarget = false;
        t.enableAutoSizing = true; t.fontSizeMin = Mathf.Max(14f, size * 0.5f); t.fontSizeMax = size;
        return t;
    }

    Button Btn(Transform p, string s, float size, Color bg, Color fg, float x0, float y0, float x1, float y1,
               UnityAction act, out TextMeshProUGUI label, bool head = true)
    {
        // soft shadow under the button
        var sh = Card(p, new Color(0f, 0f, 0f, 0.16f), x0, y0, x1, y1, false);
        sh.rectTransform.offsetMin = new Vector2(0f, -9f);
        sh.rectTransform.offsetMax = new Vector2(0f, -9f);

        var img = Card(p, bg, x0, y0, x1, y1);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img; b.onClick.AddListener(act);
        img.gameObject.AddComponent<PressScale>();
        label = Txt(img.transform, s, size, TextAlignmentOptions.Center, fg, 0.04f, 0.06f, 0.96f, 0.94f, head);
        return b;
    }

    /// <summary>Full-screen container with a CanvasGroup (used for the fade-in).</summary>
    GameObject Container(string n)
    {
        var g = R(n, root, 0, 0, 1, 1).gameObject;
        fades.Add(g.AddComponent<CanvasGroup>());
        return g;
    }

    /// <summary>A child that stays inside the phone's safe area (notches, rounded corners).</summary>
    RectTransform Safe(Transform p)
    {
        var rt = R("Safe", p, 0, 0, 1, 1);
        safeRects.Add(rt);
        return rt;
    }

    void ApplySafe()
    {
        var sa = Screen.safeArea;
        lastSafe = sa;
        if (Screen.width <= 0 || Screen.height <= 0) return;
        var aMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
        var aMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
        foreach (var rt in safeRects) { rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = rt.offsetMax = Vector2.zero; }
    }

    void Show(GameObject g, bool on)
    {
        if (g.activeSelf == on) return;
        g.SetActive(on);
        if (on) { var cg = g.GetComponent<CanvasGroup>(); if (cg != null) cg.alpha = 0f; }   // fades in (see Update)
    }

    // ================= build all screens =================
    void BuildCanvas()
    {
        var cg = new GameObject("GoBagCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = cg.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
        var sc = cg.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1080, 1920); sc.matchWidthOrHeight = 0.5f;
        root = cg.transform;

        BuildStart(); BuildPrepare(); BuildHud(); BuildQuiz(); BuildResult();

        // things that float above every screen
        overlay = Safe(root);
        BuildInfoCard(); BuildChooser(); BuildToast(); BuildFloat();

        ApplySafe();
    }

    // 1. START
    void BuildStart()
    {
        startPanel = Container("Start");
        // green top, with a big peach dome rising from the bottom
        Fill(startPanel.transform, Olive, 0, 0, 1, 1);
        Blob(startPanel.transform, A(Cream, 0.10f), 0f, 1f, 640f, 60f, -60f);   // soft light bubbles on the green
        Blob(startPanel.transform, A(Cream, 0.10f), 1f, 0.62f, 360f, 120f, 0f);
        Blob(startPanel.transform, Peach, 0.5f, 0f, 2000f);                      // the peach dome
        Blob(startPanel.transform, Coral, 1f, 0f, 420f, 90f, -90f);              // coral accent in the corner
        var s = Safe(startPanel.transform);

        Txt(s, "GO-BAG", 130, TextAlignmentOptions.Center, Cream, 0.05f, 0.80f, 0.95f, 0.93f, true);
        var pill = Card(s, Peach, 0.28f, 0.745f, 0.72f, 0.80f, false);
        Txt(pill.transform, "ESSENTIALS", 40, TextAlignmentOptions.Center, Ink, 0.05f, 0.05f, 0.95f, 0.95f, true);
        Txt(s, "Find all 6 things to pack in an emergency bag!", 40, TextAlignmentOptions.Center, Cream, 0.08f, 0.665f, 0.92f, 0.735f, true);

        StepRow(s.transform, 1, "Tap START and point your camera at the Go-Bag marker.", 0.52f, 0.65f);
        StepRow(s.transform, 2, "Tap the bag, pick an item and place it. Then tap the item to earn +10 points and learn about it!", 0.365f, 0.505f);
        StepRow(s.transform, 3, "Find all 6 items, then take the earthquake quiz (+20 points for each right answer).", 0.21f, 0.35f);

        Btn(s, "START!", 66, Olive, Cream, 0.2f, 0.065f, 0.8f, 0.175f, () => gm.SetStage(Stage.Prepare), out _);
    }

    void StepRow(Transform p, int n, string text, float y0, float y1)
    {
        var row = Card(p, Cream, 0.06f, y0, 0.94f, y1, false);
        var num = Blob(row.transform, Coral, 0f, 0.5f, 110f, 80f, 0f);
        Txt(num.transform, n.ToString(), 60, TextAlignmentOptions.Center, Ink, 0f, 0f, 1f, 1f, true);
        Txt(row.transform, text, 36, TextAlignmentOptions.Left, Ink, 0.2f, 0.05f, 0.96f, 0.95f);
    }

    // 2. PREPARE
    void BuildPrepare()
    {
        preparePanel = Container("Prepare");
        var s = Safe(preparePanel.transform);
        var banner = Card(s, A(Cream, 0.95f), 0.05f, 0.79f, 0.95f, 0.94f, false);
        Txt(banner.transform, "Point your camera at the Go-Bag marker.\nHold it flat, in good light.", 40,
            TextAlignmentOptions.Center, Ink, 0.05f, 0.08f, 0.95f, 0.92f);

        // a frame that shows kids where to aim
        var frame = R("ScanFrame", s, 0.17f, 0.36f, 0.83f, 0.64f).gameObject.AddComponent<Image>();
        frame.sprite = outlineSprite; frame.type = Image.Type.Sliced; frame.pixelsPerUnitMultiplier = Corner;
        frame.color = Cream; frame.raycastTarget = false;
        scanFrame = frame;

        var wait = Card(s, A(Olive, 0.92f), 0.2f, 0.27f, 0.8f, 0.335f, false);
        Txt(wait.transform, "Looking for the marker...", 36, TextAlignmentOptions.Center, Cream, 0.05f, 0.08f, 0.95f, 0.92f, true);
    }

    // 3. INTERACT (HUD)
    void BuildHud()
    {
        hudPanel = Container("HUD");
        var s = Safe(hudPanel.transform);

        // top card: found count, points and 6 slots
        var top = Card(s, A(Cream, 0.95f), 0.03f, 0.885f, 0.97f, 0.985f);
        progressText = Txt(top.transform, "", 42, TextAlignmentOptions.Left, Olive, 0.05f, 0.5f, 0.55f, 0.95f, true);
        pointsText = Txt(top.transform, "", 42, TextAlignmentOptions.Right, Ink, 0.5f, 0.5f, 0.95f, 0.95f, true);
        int n = gm.ItemCount;
        slots = new Image[n];
        float step = 0.9f / n;
        for (int i = 0; i < n; i++)
            slots[i] = Card(top.transform, SlotEmpty, 0.05f + step * i, 0.12f, 0.05f + step * (i + 1) - 0.02f, 0.4f, false);

        // hint pill
        var hint = Card(s, A(Olive, 0.92f), 0.08f, 0.125f, 0.92f, 0.2f, false);
        hintBox = hint.gameObject;
        hintText = Txt(hint.transform, "", 34, TextAlignmentOptions.Center, Cream, 0.03f, 0.05f, 0.97f, 0.95f, true);

        // bottom bar: only shown while an item is out of the bag
        var bar = Card(s, A(Cream, 0.95f), 0.03f, 0.012f, 0.97f, 0.112f);
        viewBar = bar.gameObject;
        string[] labels = { "Bag", "Turn\n<", "Turn\n>", "Zoom\n-", "Zoom\n+", "Info" };
        UnityAction[] acts =
        {
            OpenChooser,
            () => spawner.Viewer.Rotate(-45f), () => spawner.Viewer.Rotate(45f),
            () => spawner.Viewer.Zoom(0.8f),   () => spawner.Viewer.Zoom(1.25f),
            ReopenInfo
        };
        for (int i = 0; i < 6; i++)
        {
            float x0 = 0.015f + 0.1625f * i;
            Btn(bar.transform, labels[i], 32, i == 0 ? Olive : (i == 5 ? Coral : Peach), i == 0 ? Cream : Ink,
                x0, 0.12f, x0 + 0.15f, 0.88f, acts[i], out _);
        }
        viewBar.SetActive(false);
    }

    // Info card (slides up from the bottom)
    void BuildInfoCard()
    {
        cardRect = R("InfoCard", overlay, 0.03f, 0.12f, 0.97f, 0.52f);
        infoCard = cardRect.gameObject;
        cardMin = cardRect.anchorMin; cardMax = cardRect.anchorMax;

        Card(cardRect, Cream, 0, 0, 1, 1);
        infoTitle = Txt(cardRect, "", 56, TextAlignmentOptions.Left, Olive, 0.05f, 0.80f, 0.62f, 0.97f, true);
        infoPtsBox = Card(cardRect, Coral, 0.64f, 0.82f, 0.95f, 0.95f, false);
        infoPts = Txt(infoPtsBox.transform, "", 32, TextAlignmentOptions.Center, Ink, 0.04f, 0.05f, 0.96f, 0.95f, true);
        infoBody = Txt(cardRect, "", 36, TextAlignmentOptions.TopLeft, Ink, 0.05f, 0.44f, 0.95f, 0.78f);
        var tipBox = Card(cardRect, A(Peach, 0.55f), 0.05f, 0.19f, 0.95f, 0.42f, false);
        infoTip = Txt(tipBox.transform, "", 34, TextAlignmentOptions.Left, Ink, 0.04f, 0.05f, 0.96f, 0.95f);
        Btn(cardRect, "Got it!", 44, Coral, Ink, 0.3f, 0.03f, 0.7f, 0.16f, HideCard, out _);
        infoCard.SetActive(false);
    }

    // 4. QUIZ
    void BuildQuiz()
    {
        quizPanel = Container("Quiz");
        Fill(quizPanel.transform, Cream, 0, 0, 1, 1);
        var s = Safe(quizPanel.transform);

        var hp = Card(s, Olive, 0.05f, 0.915f, 0.5f, 0.975f, false);
        quizHeader = Txt(hp.transform, "", 34, TextAlignmentOptions.Center, Cream, 0.05f, 0.05f, 0.95f, 0.95f, true);
        var pp = Card(s, Coral, 0.6f, 0.915f, 0.95f, 0.975f, false);
        quizPoints = Txt(pp.transform, "", 34, TextAlignmentOptions.Center, Ink, 0.05f, 0.05f, 0.95f, 0.95f, true);

        var qc = Card(s, Olive, 0.05f, 0.71f, 0.95f, 0.9f, false);
        quizQuestion = Txt(qc.transform, "", 46, TextAlignmentOptions.Center, Cream, 0.05f, 0.08f, 0.95f, 0.92f, true);

        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            float yMax = 0.675f - 0.115f * i;
            optionButtons[i] = Btn(s, "", 38, Peach, Ink, 0.05f, yMax - 0.1f, 0.95f, yMax, () => OnAnswer(idx), out optionTexts[i], false);
        }

        var fb = Card(s, A(Peach, 0.45f), 0.05f, 0.105f, 0.95f, 0.215f, false);
        feedbackBox = fb.gameObject;
        quizFeedback = Txt(fb.transform, "", 34, TextAlignmentOptions.Center, Ink, 0.04f, 0.06f, 0.96f, 0.94f);
        nextButton = Btn(s, "Next", 46, Coral, Ink, 0.25f, 0.02f, 0.75f, 0.095f, OnNext, out nextLabel);
    }

    // 5. COMPLETE
    void BuildResult()
    {
        resultPanel = Container("Result");
        // same look as the home screen: green top, peach dome at the bottom
        Fill(resultPanel.transform, Olive, 0, 0, 1, 1);
        Blob(resultPanel.transform, A(Cream, 0.10f), 0f, 1f, 640f, 60f, -60f);
        Blob(resultPanel.transform, Peach, 0.5f, 0f, 1700f);
        Blob(resultPanel.transform, Coral, 1f, 0f, 380f, 90f, -90f);
        var s = Safe(resultPanel.transform);

        Txt(s, "MISSION COMPLETE!", 84, TextAlignmentOptions.Center, Cream, 0.04f, 0.87f, 0.96f, 0.975f, true);

        // score ring: fills up to your completion percent
        Blob(s, Cream, 0.5f, 0.70f, 580f);
        var track = Blob(s, A(Peach, 0.7f), 0.5f, 0.70f, 500f);
        track.sprite = ringSprite;
        ringFill = Blob(s, Coral, 0.5f, 0.70f, 500f);
        ringFill.sprite = ringSprite;
        ringFill.type = Image.Type.Filled;
        ringFill.fillMethod = Image.FillMethod.Radial360;
        ringFill.fillOrigin = (int)Image.Origin360.Top;
        ringFill.fillClockwise = true;
        resultScore = Txt(s, "", 130, TextAlignmentOptions.Center, Olive, 0.3f, 0.675f, 0.7f, 0.77f, true);
        resultOutOf = Txt(s, "", 30, TextAlignmentOptions.Center, Ink, 0.3f, 0.635f, 0.7f, 0.68f);
        resultRank = Txt(s, "", 66, TextAlignmentOptions.Center, Cream, 0.05f, 0.49f, 0.95f, 0.565f, true);

        // three little stat cards
        string[] statNames = { "Items found", "Quiz score", "Complete" };
        statValue = new TextMeshProUGUI[3];
        for (int i = 0; i < 3; i++)
        {
            float x0 = 0.06f + i * 0.31f;
            var chip = Card(s, Cream, x0, 0.385f, x0 + 0.26f, 0.485f, false);
            statValue[i] = Txt(chip.transform, "", 54, TextAlignmentOptions.Center, Olive, 0.05f, 0.40f, 0.95f, 0.95f, true);
            Txt(chip.transform, statNames[i], 26, TextAlignmentOptions.Center, Ink, 0.05f, 0.06f, 0.95f, 0.42f);
        }

        // badges: earned ones are coral, the others are faded
        var bc = Card(s, Cream, 0.06f, 0.195f, 0.94f, 0.37f, false);
        Txt(bc.transform, "Badges", 34, TextAlignmentOptions.Left, Olive, 0.05f, 0.80f, 0.5f, 0.98f, true);
        badgeBg = new Image[BadgeTitles.Length];
        badgeText = new TextMeshProUGUI[BadgeTitles.Length];
        for (int i = 0; i < BadgeTitles.Length; i++)
        {
            float x0 = 0.03f + (i % 2) * 0.485f;
            float y1 = 0.77f - (i / 2) * 0.375f;
            var pill = Card(bc.transform, Peach, x0, y1 - 0.35f, x0 + 0.455f, y1, false);
            badgeBg[i] = pill;
            badgeText[i] = Txt(pill.transform, "", 30, TextAlignmentOptions.Center, Ink, 0.04f, 0.04f, 0.96f, 0.96f, true);
        }

        Btn(s, "Keep Exploring", 38, Olive, Cream, 0.06f, 0.055f, 0.49f, 0.155f, () => gm.SetStage(Stage.Interact), out _);
        Btn(s, "Play Again", 38, Cream, Ink, 0.51f, 0.055f, 0.94f, 0.155f, () => gm.ResetGame(), out _);
    }

    void BuildToast()
    {
        var box = Card(overlay, A(Olive, 0.95f), 0.08f, 0.80f, 0.92f, 0.87f, false);
        toastBox = box.gameObject;
        toastText = Txt(box.transform, "", 34, TextAlignmentOptions.Center, Cream, 0.04f, 0.06f, 0.96f, 0.94f, true);
        toastBox.SetActive(false);
    }

    // "+10 pts" bubble that floats up
    void BuildFloat()
    {
        floatRect = R("Float", overlay, 0.34f, 0.60f, 0.66f, 0.67f);
        floatGroup = floatRect.gameObject.AddComponent<CanvasGroup>();
        floatGroup.blocksRaycasts = false; floatGroup.interactable = false;
        Card(floatRect, Coral, 0, 0, 1, 1, false);
        floatText = Txt(floatRect, "", 52, TextAlignmentOptions.Center, Ink, 0.05f, 0.05f, 0.95f, 0.95f, true);
        floatRect.gameObject.SetActive(false);
    }

    // ================= behaviour =================
    void Refresh()
    {
        var s = gm.CurrentStage;
        Show(startPanel, s == Stage.Start);
        Show(preparePanel, s == Stage.Prepare);
        Show(hudPanel, s == Stage.Interact);
        Show(quizPanel, s == Stage.Quiz);
        Show(resultPanel, s == Stage.Complete);
        if (s != Stage.Interact)
        {
            infoCard.SetActive(false);
            chooserOpen = false; chooserRect.gameObject.SetActive(false);
            if (spawner != null && spawner.Viewer != null) spawner.Viewer.Clear();
        }

        progressText.text = "Found " + gm.FoundCount + "/" + gm.ItemCount;
        pointsText.text = gm.Points + " pts";
        for (int i = 0; i < slots.Length; i++) slots[i].color = gm.IsFound(i) ? Coral : SlotEmpty;

        if (chooserOpen) RefreshChooser();
        UpdateHint();
        if (s == Stage.Complete) ShowResults();
    }

    void UpdateHint()
    {
        if (hintBox == null || infoCard == null || spawner == null) return;
        string msg = null;
        var v = spawner.Viewer;
        if (gm.CurrentStage == Stage.Interact && !chooserOpen && !infoCard.activeSelf)
        {
            if (v != null && v.Placing) msg = "Move your phone, then TAP the table to place it!";
            else if (v != null && v.HasItem)
            {
                string nm = GoBagData.Items[v.Index].name;
                msg = gm.IsFound(v.Index) ? "Tap the " + nm + " to read about it again!"
                                          : "Tap the " + nm + " to earn points and see its info!";
            }
            else if (!gm.AllFound) msg = "Tap the go-bag to open it!";
            else if (!gm.QuizDone) msg = "You found them all! Tap the bag for the quiz!";
            else msg = "Tap the bag to look at the items again!";
        }
        hintBox.SetActive(msg != null);
        if (msg != null && hintText.text != msg) hintText.text = msg;
    }

    void HideCard()
    {
        infoCard.SetActive(false);
        ItemHotspot.PopNone();
        UpdateHint();
    }

    // ---- item chooser: opens when the bag is tapped ----
    void BuildChooser()
    {
        chooserRect = R("Chooser", overlay, 0.03f, 0.10f, 0.97f, 0.66f);
        chooserMin = chooserRect.anchorMin; chooserMax = chooserRect.anchorMax;
        Card(chooserRect, Cream, 0, 0, 1, 1);
        Txt(chooserRect, "Open the Go-Bag!", 60, TextAlignmentOptions.Center, Olive, 0.05f, 0.87f, 0.95f, 0.98f, true);
        Txt(chooserRect, "Pick an essential, then place it in AR.", 32, TextAlignmentOptions.Center, Ink, 0.05f, 0.80f, 0.95f, 0.87f);

        int n = gm.ItemCount;
        chooserBg = new Image[n]; chooserName = new TextMeshProUGUI[n]; chooserFound = new GameObject[n];
        for (int i = 0; i < n; i++)
        {
            int idx = i;
            float x0 = 0.04f + (i % 3) * 0.315f;
            float y1 = 0.79f - (i / 3) * 0.31f;
            var b = Btn(chooserRect, GoBagData.Items[i].name, 34, Peach, Ink, x0, y1 - 0.29f, x0 + 0.29f, y1,
                        () => PickItem(idx), out var label);
            chooserBg[i] = b.image; chooserName[i] = label;
            label.rectTransform.anchorMin = new Vector2(0.04f, 0.03f);     // name sits under the picture
            label.rectTransform.anchorMax = new Vector2(0.96f, 0.27f);

            var th = R("Thumb", b.transform, 0.08f, 0.29f, 0.92f, 0.96f).gameObject.AddComponent<Image>();
            th.sprite = ItemLibrary.Thumbnail(i); th.preserveAspect = true; th.raycastTarget = false;

            var fp = Card(b.transform, Coral, 0.40f, 0.85f, 0.97f, 0.98f, false);
            Txt(fp.transform, "Found!", 24, TextAlignmentOptions.Center, Ink, 0.05f, 0.05f, 0.95f, 0.95f, true);
            chooserFound[i] = fp.gameObject;
        }
        Btn(chooserRect, "Close", 44, Olive, Cream, 0.25f, 0.02f, 0.75f, 0.16f, OnChooserBottom, out chooserBottomLabel);
        chooserRect.gameObject.SetActive(false);
    }

    void RefreshChooser()
    {
        for (int i = 0; i < chooserBg.Length; i++)
        {
            bool f = gm.IsFound(i);
            chooserBg[i].color = f ? FoundTint : Peach;
            chooserFound[i].SetActive(f);
        }
        chooserBottomLabel.text = gm.AllFound ? (gm.QuizDone ? "See Results" : "Take the Quiz!") : "Close";
    }

    public void OpenChooser()
    {
        if (gm.CurrentStage != Stage.Interact || spawner == null || spawner.Bag == null) return;
        infoCard.SetActive(false);
        spawner.Viewer.Clear();               // the item goes back into the bag
        chooserOpen = true;
        RefreshChooser();
        chooserRect.gameObject.SetActive(true);
        chooserT = 0f; Slide(chooserRect, chooserMin, chooserMax, 0f);
        UpdateHint();
    }

    void CloseChooser()
    {
        chooserOpen = false;
        chooserRect.gameObject.SetActive(false);
        UpdateHint();
    }

    void OnChooserBottom()
    {
        if (gm.AllFound) OnQuizButton(); else CloseChooser();
    }

    void PickItem(int i)
    {
        CloseChooser();
        infoCard.SetActive(false);
        spawner.Viewer.Show(i, spawner.BagTransform, spawner.MarkerSize);   // hovers in AR until the player taps
        UpdateHint();
    }

    static void Slide(RectTransform r, Vector2 min, Vector2 max, float t)
    {
        float e = 1f - Mathf.Pow(1f - t, 3f);
        float shift = (1f - e) * -0.7f;
        r.anchorMin = new Vector2(min.x, min.y + shift);
        r.anchorMax = new Vector2(max.x, max.y + shift);
    }

    // ---- item info ----
    /// <summary>Called when the player taps the item they placed in AR: awards points the first time and shows its info.</summary>
    public void OnItemTapped(int i)
    {
        bool isNew = gm.Discover(i);
        FillInfo(i, isNew);
        if (isNew) Pop("+" + GoBagData.PointsPerItem + " pts");
        if (isNew && gm.AllFound) Toast("All items found! Tap the bag to take the quiz.");
    }

    void ReopenInfo()
    {
        if (!spawner.Viewer.HasItem) return;
        int i = spawner.Viewer.Index;
        if (gm.IsFound(i)) FillInfo(i, false);
        else Toast("Tap the item in AR to earn points and see its info!");
    }

    void FillInfo(int i, bool isNew)
    {
        var item = GoBagData.Items[i];
        infoTitle.text = item.name;
        infoBody.text = item.info;
        infoTip.text = "<color=" + HexOf(Olive) + ">Tip:</color> " + item.tip;
        infoPts.text = isNew ? "NEW! +" + GoBagData.PointsPerItem + " pts" : "Already found";
        infoPtsBox.color = isNew ? Coral : Peach;
        infoCard.SetActive(true);
        cardT = 0f; ApplyCard(0f);          // slide the card up
        UpdateHint();
    }

    void OnQuizButton()
    {
        if (gm.QuizDone) { gm.SetStage(Stage.Complete); return; }
        if (!gm.AllFound) { Toast("Find all " + gm.ItemCount + " items to unlock the quiz!"); return; }
        qIndex = 0;
        gm.SetStage(Stage.Quiz);
        ShowQuestion();
    }

    void ShowQuestion()
    {
        answered = false;
        var q = GoBagData.Quiz[qIndex];
        quizHeader.text = "Question " + (qIndex + 1) + "/" + GoBagData.Quiz.Length;
        quizPoints.text = gm.Points + " pts";
        quizQuestion.text = q.text;
        for (int i = 0; i < 4; i++)
        {
            optionTexts[i].text = q.options[i];
            optionTexts[i].color = Ink;
            optionButtons[i].image.color = Peach;
        }
        quizFeedback.text = "";
        feedbackBox.SetActive(false);
        nextButton.gameObject.SetActive(false);
    }

    void OnAnswer(int i)
    {
        if (answered) return;
        answered = true;
        var q = GoBagData.Quiz[qIndex];
        bool ok = i == q.answer;
        gm.AnswerQuiz(ok);

        optionButtons[q.answer].image.color = Olive;
        optionTexts[q.answer].color = Cream;
        if (!ok) { optionButtons[i].image.color = Berry; optionTexts[i].color = Cream; }

        quizFeedback.text = (ok ? "Correct! +" + GoBagData.PointsPerAnswer + " points\n" : "Not quite.\n") + q.explain;
        quizFeedback.color = ok ? Hex("#3F4C2A") : Hex("#8F351F");
        feedbackBox.SetActive(true);
        quizPoints.text = gm.Points + " pts";
        nextLabel.text = qIndex == GoBagData.Quiz.Length - 1 ? "See Results" : "Next Question";
        nextButton.gameObject.SetActive(true);
        if (ok) Pop("+" + GoBagData.PointsPerAnswer + "!");
    }

    void OnNext()
    {
        qIndex++;
        if (qIndex >= GoBagData.Quiz.Length) gm.FinishQuiz();
        else ShowQuestion();
    }

    void ShowResults()
    {
        ringShown = 0f;                                   // the ring fills up from empty (see Update)
        resultScore.text = gm.Points.ToString();
        resultOutOf.text = "out of " + gm.MaxPoints + " points";
        resultRank.text = gm.Rank;
        statValue[0].text = gm.FoundCount + "/" + gm.ItemCount;
        statValue[1].text = gm.QuizCorrect + "/" + GoBagData.Quiz.Length;
        statValue[2].text = Mathf.RoundToInt(gm.Percent * 100f) + "%";

        var earned = gm.Achievements();
        for (int i = 0; i < BadgeTitles.Length; i++)
        {
            bool got = earned.Exists(a => a.StartsWith(BadgeTitles[i]));
            badgeBg[i].color = got ? Coral : A(Peach, 0.35f);
            badgeText[i].color = got ? Ink : A(Ink, 0.5f);
            badgeText[i].text = BadgeTitles[i] + "\n<size=72%>" + (got ? BadgeDescs[i] : "Not yet!") + "</size>";
        }
    }

    void Toast(string msg) { toastText.text = msg; toastBox.SetActive(true); toastTimer = 2.5f; }

    void Pop(string msg)
    {
        floatText.text = msg;
        floatT = 0f;
        floatRect.anchoredPosition = Vector2.zero;
        floatGroup.alpha = 1f;
        floatRect.gameObject.SetActive(true);
    }

    void ApplyCard(float t)
    {
        float e = 1f - Mathf.Pow(1f - t, 3f);          // smooth slide-up
        float shift = (1f - e) * -0.6f;
        cardRect.anchorMin = new Vector2(cardMin.x, cardMin.y + shift);
        cardRect.anchorMax = new Vector2(cardMax.x, cardMax.y + shift);
    }

    void Update()
    {
        if (toastTimer > 0f)
        {
            toastTimer -= Time.deltaTime;
            if (toastTimer <= 0f) toastBox.SetActive(false);
        }

        if (cardT < 1f)
        {
            cardT = Mathf.Min(1f, cardT + Time.deltaTime * 3.5f);
            ApplyCard(cardT);
        }

        if (chooserT < 1f)
        {
            chooserT = Mathf.Min(1f, chooserT + Time.deltaTime * 3.5f);
            Slide(chooserRect, chooserMin, chooserMax, chooserT);
        }

        // the Turn / Zoom bar only shows while an item is out of the bag
        bool showBar = gm.CurrentStage == Stage.Interact && !chooserOpen && spawner != null && spawner.Viewer != null && spawner.Viewer.HasItem;
        if (viewBar.activeSelf != showBar) viewBar.SetActive(showBar);
        if (hudPanel.activeSelf) UpdateHint();

        if (floatRect.gameObject.activeSelf)
        {
            floatT += Time.deltaTime / 1.2f;
            floatRect.anchoredPosition = new Vector2(0f, floatT * 160f);
            floatGroup.alpha = 1f - Mathf.Clamp01((floatT - 0.6f) / 0.4f);
            if (floatT >= 1f) floatRect.gameObject.SetActive(false);
        }

        // screens fade in
        foreach (var cg in fades)
            if (cg.gameObject.activeInHierarchy && cg.alpha < 1f)
                cg.alpha = Mathf.MoveTowards(cg.alpha, 1f, Time.deltaTime * 5f);

        // the score ring fills up on the summary screen
        if (resultPanel.activeSelf && ringFill != null)
        {
            ringShown = Mathf.MoveTowards(ringShown, gm.Percent, Time.deltaTime * 0.6f);
            ringFill.fillAmount = ringShown;
        }

        // pulsing aim frame while waiting for the marker
        if (preparePanel.activeSelf)
            scanFrame.color = A(Cream, 0.55f + 0.4f * Mathf.Sin(Time.time * 3f));

        if (Screen.safeArea != lastSafe) ApplySafe();
    }
}

/// <summary>Makes a button shrink a little while it is pressed.</summary>
public class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public void OnPointerDown(PointerEventData e) { transform.localScale = Vector3.one * 0.95f; }
    public void OnPointerUp(PointerEventData e) { transform.localScale = Vector3.one; }
    public void OnPointerExit(PointerEventData e) { transform.localScale = Vector3.one; }
}
