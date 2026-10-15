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
    Sprite roundSprite, outlineSprite, circleSprite;
    Transform root;
    RectTransform overlay;

    GameObject startPanel, preparePanel, hudPanel, infoCard, quizPanel, resultPanel, toastBox, hintBox, feedbackBox;
    TextMeshProUGUI progressText, pointsText, toastText, hintText, infoTitle, infoBody, infoTip, infoPts,
        quizHeader, quizPoints, quizQuestion, quizFeedback, resultText, quizBtnLabel, nextLabel, floatText;
    Image infoPtsBox, scanFrame;
    Image[] slots;
    RectTransform cardRect, floatRect;
    CanvasGroup floatGroup;
    Button quizBtn, nextButton;
    Button[] optionButtons = new Button[4];
    TextMeshProUGUI[] optionTexts = new TextMeshProUGUI[4];

    readonly List<CanvasGroup> fades = new List<CanvasGroup>();
    readonly List<RectTransform> safeRects = new List<RectTransform>();
    Rect lastSafe;
    Vector2 cardMin, cardMax;
    int qIndex; bool answered;
    float toastTimer, cardT = 1f, floatT = 1f;

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
        BuildInfoCard(); BuildToast(); BuildFloat();

        ApplySafe();
    }

    // 1. START
    void BuildStart()
    {
        startPanel = Container("Start");
        Fill(startPanel.transform, Cream, 0, 0, 1, 1);
        Blob(startPanel.transform, Coral, 1f, 1f, 560);
        Blob(startPanel.transform, Peach, 0f, 0f, 520);
        var s = Safe(startPanel.transform);

        Txt(s, "GO-BAG", 130, TextAlignmentOptions.Center, Olive, 0.05f, 0.80f, 0.95f, 0.93f, true);
        var pill = Card(s, Coral, 0.28f, 0.745f, 0.72f, 0.80f, false);
        Txt(pill.transform, "ESSENTIALS", 40, TextAlignmentOptions.Center, Ink, 0.05f, 0.05f, 0.95f, 0.95f, true);
        Txt(s, "Find all 6 things to pack in an emergency bag!", 40, TextAlignmentOptions.Center, Ink, 0.08f, 0.665f, 0.92f, 0.735f, true);

        StepRow(s.transform, 1, "Tap START and point your camera at the Go-Bag marker.", 0.52f, 0.65f);
        StepRow(s.transform, 2, "Tap the items to learn about them (+10 points). Use the buttons to turn, zoom and change colors.", 0.365f, 0.505f);
        StepRow(s.transform, 3, "Find all 6 items, then take the quiz (+20 points for each right answer).", 0.21f, 0.35f);

        Btn(s, "START!", 66, Coral, Ink, 0.2f, 0.065f, 0.8f, 0.175f, () => gm.SetStage(Stage.Prepare), out _);
    }

    void StepRow(Transform p, int n, string text, float y0, float y1)
    {
        var row = Card(p, A(Peach, 0.45f), 0.06f, y0, 0.94f, y1, false);
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
        var hint = Card(s, A(Olive, 0.92f), 0.1f, 0.128f, 0.9f, 0.188f, false);
        hintBox = hint.gameObject;
        hintText = Txt(hint.transform, "", 34, TextAlignmentOptions.Center, Cream, 0.03f, 0.05f, 0.97f, 0.95f, true);

        // bottom bar
        var bar = Card(s, A(Cream, 0.95f), 0.03f, 0.012f, 0.97f, 0.112f);
        string[] labels = { "Turn\n<", "Turn\n>", "Zoom\n-", "Zoom\n+", "Color" };
        UnityAction[] acts =
        {
            () => spawner.Product?.Rotate(-45f), () => spawner.Product?.Rotate(45f),
            () => spawner.Product?.Zoom(0.8f),   () => spawner.Product?.Zoom(1.25f),
            () => { if (spawner.Product != null) Toast("Bag color: " + spawner.Product.CycleColor()); }
        };
        for (int i = 0; i < 5; i++)
        {
            float x0 = 0.02f + 0.128f * i;
            Btn(bar.transform, labels[i], 32, i == 4 ? Coral : Peach, Ink, x0, 0.12f, x0 + 0.118f, 0.88f, acts[i], out _);
        }
        quizBtn = Btn(bar.transform, "Quiz", 38, Olive, Cream, 0.68f, 0.12f, 0.98f, 0.88f, OnQuizButton, out quizBtnLabel);
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
        Fill(resultPanel.transform, Cream, 0, 0, 1, 1);
        Blob(resultPanel.transform, Coral, 1f, 1f, 560);
        Blob(resultPanel.transform, Peach, 0f, 0f, 520);
        var s = Safe(resultPanel.transform);

        Txt(s, "MISSION\nCOMPLETE!", 90, TextAlignmentOptions.Center, Olive, 0.05f, 0.77f, 0.95f, 0.94f, true);
        var card = Card(s, A(Cream, 0.97f), 0.06f, 0.3f, 0.94f, 0.76f, false);
        resultText = Txt(card.transform, "", 42, TextAlignmentOptions.Top, Ink, 0.06f, 0.04f, 0.94f, 0.96f);
        Btn(s, "Keep Exploring", 38, Olive, Cream, 0.06f, 0.15f, 0.49f, 0.26f, () => gm.SetStage(Stage.Interact), out _);
        Btn(s, "Play Again", 38, Coral, Ink, 0.51f, 0.15f, 0.94f, 0.26f, () => gm.ResetGame(), out _);
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
        if (s != Stage.Interact) { infoCard.SetActive(false); ItemHotspot.PopNone(); }

        progressText.text = "Found " + gm.FoundCount + "/" + gm.ItemCount;
        pointsText.text = gm.Points + " pts";
        for (int i = 0; i < slots.Length; i++) slots[i].color = gm.IsFound(i) ? Coral : SlotEmpty;

        bool locked = !gm.AllFound && !gm.QuizDone;
        quizBtn.image.color = locked ? A(Olive, 0.5f) : Olive;
        quizBtnLabel.color = locked ? Ink : Cream;
        quizBtnLabel.text = gm.QuizDone ? "Results" : (gm.AllFound ? "Quiz!" : "Quiz\n(locked)");

        UpdateHint();
        if (s == Stage.Complete) ShowResults();
    }

    void UpdateHint()
    {
        if (hintBox == null || infoCard == null) return;
        string msg = null;
        if (!infoCard.activeSelf)
        {
            if (!gm.AllFound) msg = "Tap an item to learn about it!";
            else if (!gm.QuizDone) msg = "You found them all! Tap Quiz!";
        }
        hintBox.SetActive(msg != null);
        if (msg != null) hintText.text = msg;
    }

    void HideCard()
    {
        infoCard.SetActive(false);
        ItemHotspot.PopNone();
        UpdateHint();
    }

    public void ShowItem(int i)
    {
        var item = GoBagData.Items[i];
        bool isNew = gm.Discover(i);
        infoTitle.text = item.name;
        infoBody.text = item.info;
        infoTip.text = "<color=" + HexOf(Olive) + ">Tip:</color> " + item.tip;
        infoPts.text = isNew ? "NEW! +" + GoBagData.PointsPerItem + " pts" : "Already found";
        infoPtsBox.color = isNew ? Coral : Peach;
        infoCard.SetActive(true);
        cardT = 0f; ApplyCard(0f);          // slide the card up
        ItemHotspot.PopOnly(i);             // pop the 3D item
        UpdateHint();
        if (isNew) Pop("+" + GoBagData.PointsPerItem + " pts");
        if (isNew && gm.AllFound) Toast("All items found! The quiz is unlocked.");
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
        string green = HexOf(Olive);
        string s = "<size=130%><color=" + green + ">" + gm.Rank + "</color></size>\n\n" +
                   "Score: " + gm.Points + " / " + gm.MaxPoints +
                   "\nItems found: " + gm.FoundCount + "/" + gm.ItemCount +
                   "\nQuiz: " + gm.QuizCorrect + "/" + GoBagData.Quiz.Length +
                   "\n\n<color=" + green + ">Badges</color>";
        foreach (var a in gm.Achievements()) s += "\n- " + a;
        resultText.text = s;
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
