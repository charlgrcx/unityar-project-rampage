using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Builds the whole UI from code (no prefabs / no manual wiring) and runs the 6-step flow:
/// Start -> Prepare -> Interact -> Challenge (quiz) -> Feedback -> Complete.
/// </summary>
public class UIManager : MonoBehaviour
{
    GameManager gm;
    MarkerSpawner spawner;
    Font font;
    Transform root;

    GameObject startPanel, preparePanel, hudPanel, infoCard, quizPanel, resultPanel, toastBox;
    Text progressText, pointsText, toastText, infoTitle, infoBody, infoPts, quizHeader, quizQuestion, quizFeedback, resultText, quizBtnLabel;
    RectTransform progressFill;
    Button nextButton;
    Button[] optionButtons = new Button[4];
    Text[] optionTexts = new Text[4];
    int qIndex; bool answered; float toastTimer; float cardT = 1f;

    static readonly Color Panel = new Color(0.05f, 0.08f, 0.12f, 0.93f);
    static readonly Color Bar = new Color(0f, 0f, 0f, 0.6f);
    static readonly Color Green = new Color(0.15f, 0.6f, 0.3f);
    static readonly Color Blue = new Color(0.15f, 0.4f, 0.75f);
    static readonly Color Gold = new Color(1f, 0.82f, 0.2f);

    // ================= setup =================
    void Start()
    {
        gm = GameManager.Instance;
        spawner = GetComponent<MarkerSpawner>();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();
        BuildCanvas();
        gm.Changed += Refresh;
        Refresh();
    }

    void OnDestroy() { if (gm != null) gm.Changed -= Refresh; }

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

    Image Box(Transform p, Color c, float x0, float y0, float x1, float y1, bool raycast = true)
    {
        var img = R("Box", p, x0, y0, x1, y1).gameObject.AddComponent<Image>();
        img.color = c; img.raycastTarget = raycast;
        return img;
    }

    Text Txt(Transform p, string s, int size, TextAnchor a, Color c, float x0, float y0, float x1, float y1)
    {
        var t = R("Text", p, x0, y0, x1, y1).gameObject.AddComponent<Text>();
        t.font = font; t.text = s; t.fontSize = size; t.alignment = a; t.color = c; t.raycastTarget = false;
        t.resizeTextForBestFit = true; t.resizeTextMinSize = 18; t.resizeTextMaxSize = size;
        return t;
    }

    Button Btn(Transform p, string s, int size, Color c, float x0, float y0, float x1, float y1, UnityAction act, out Text label)
    {
        var img = Box(p, c, x0, y0, x1, y1);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img; b.onClick.AddListener(act);
        label = Txt(img.transform, s, size, TextAnchor.MiddleCenter, Color.white, 0.03f, 0.05f, 0.97f, 0.95f);
        return b;
    }

    Button Btn(Transform p, string s, int size, Color c, float x0, float y0, float x1, float y1, UnityAction act)
    { return Btn(p, s, size, c, x0, y0, x1, y1, act, out _); }

    GameObject Container(string n) { var g = R(n, root, 0, 0, 1, 1).gameObject; return g; }

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

        BuildStart(); BuildPrepare(); BuildHud(); BuildInfoCard(); BuildQuiz(); BuildResult(); BuildToast();
    }

    // 1. START
    void BuildStart()
    {
        startPanel = Container("Start");
        Box(startPanel.transform, Panel, 0, 0, 1, 1);
        var t = Txt(startPanel.transform, "GO-BAG ESSENTIALS", 80, TextAnchor.MiddleCenter, Gold, 0.05f, 0.78f, 0.95f, 0.9f);
        t.fontStyle = FontStyle.Bold;
        Txt(startPanel.transform, "AR Product Showcase", 44, TextAnchor.MiddleCenter, Color.white, 0.05f, 0.72f, 0.95f, 0.78f);
        Txt(startPanel.transform, "OBJECTIVE\nDiscover all 6 emergency essentials inside the go-bag, then pass the quiz to earn your Preparedness rank.",
            38, TextAnchor.UpperLeft, Color.white, 0.08f, 0.5f, 0.92f, 0.7f);
        Txt(startPanel.transform, "HOW TO PLAY\n1. Tap START and point the camera at the printed marker.\n2. Tap the items to learn about them (+10 pts).\n3. Use the buttons to rotate, zoom and change the bag color.\n4. Find all 6 items to unlock the quiz (+20 pts each).",
            34, TextAnchor.UpperLeft, Color.white, 0.08f, 0.23f, 0.92f, 0.5f);
        Btn(startPanel.transform, "START", 56, Green, 0.2f, 0.08f, 0.8f, 0.18f, () => gm.SetStage(Stage.Prepare));
    }

    // 2. PREPARE
    void BuildPrepare()
    {
        preparePanel = Container("Prepare");
        Box(preparePanel.transform, Bar, 0.05f, 0.78f, 0.95f, 0.93f, false);
        Txt(preparePanel.transform, "Point your camera at the GO-BAG marker.\nKeep it flat, well lit and fully in view.", 40,
            TextAnchor.MiddleCenter, Color.white, 0.08f, 0.79f, 0.92f, 0.92f);
    }

    // 3. INTERACT (HUD)
    void BuildHud()
    {
        hudPanel = Container("HUD");
        var top = Box(hudPanel.transform, Bar, 0, 0.9f, 1, 1);
        progressText = Txt(top.transform, "", 38, TextAnchor.MiddleLeft, Color.white, 0.04f, 0.45f, 0.5f, 0.95f);
        pointsText = Txt(top.transform, "", 38, TextAnchor.MiddleRight, Gold, 0.5f, 0.45f, 0.96f, 0.95f);
        Box(top.transform, new Color(1, 1, 1, 0.25f), 0.04f, 0.15f, 0.96f, 0.35f, false);
        progressFill = Box(top.transform, Green, 0.04f, 0.15f, 0.04f, 0.35f, false).rectTransform;

        var bar = Box(hudPanel.transform, Bar, 0, 0, 1, 0.11f);
        string[] labels = { "< Rotate", "Rotate >", "Zoom -", "Zoom +", "Color", "Quiz" };
        UnityAction[] acts =
        {
            () => spawner.Product?.Rotate(-45f), () => spawner.Product?.Rotate(45f),
            () => spawner.Product?.Zoom(0.8f),   () => spawner.Product?.Zoom(1.25f),
            () => { if (spawner.Product != null) Toast("Bag color: " + spawner.Product.CycleColor()); },
            OnQuizButton
        };
        for (int i = 0; i < 6; i++)
        {
            float x0 = i / 6f + 0.004f, x1 = (i + 1) / 6f - 0.004f;
            var b = Btn(bar.transform, labels[i], 30, i == 5 ? Green : Blue, x0, 0.12f, x1, 0.88f, acts[i], out var lbl);
            if (i == 5) quizBtnLabel = lbl;
        }
    }

    // 4/5. Feature info card (feedback)
    void BuildInfoCard()
    {
        infoCard = R("InfoCard", root, 0.05f, 0.13f, 0.95f, 0.46f).gameObject;
        Box(infoCard.transform, Panel, 0, 0, 1, 1);
        infoTitle = Txt(infoCard.transform, "", 52, TextAnchor.MiddleLeft, Gold, 0.05f, 0.78f, 0.95f, 0.97f);
        infoTitle.fontStyle = FontStyle.Bold;
        infoBody = Txt(infoCard.transform, "", 36, TextAnchor.UpperLeft, Color.white, 0.05f, 0.27f, 0.95f, 0.78f);
        infoPts = Txt(infoCard.transform, "", 34, TextAnchor.MiddleLeft, new Color(0.4f, 1f, 0.5f), 0.05f, 0.03f, 0.6f, 0.25f);
        Btn(infoCard.transform, "Close", 36, Blue, 0.65f, 0.04f, 0.95f, 0.24f, () => { infoCard.SetActive(false); ItemHotspot.PopNone(); });
    }

    // 4. CHALLENGE (quiz)
    void BuildQuiz()
    {
        quizPanel = Container("Quiz");
        Box(quizPanel.transform, Panel, 0, 0, 1, 1);
        quizHeader = Txt(quizPanel.transform, "", 40, TextAnchor.MiddleCenter, Gold, 0.05f, 0.9f, 0.95f, 0.97f);
        quizQuestion = Txt(quizPanel.transform, "", 48, TextAnchor.MiddleLeft, Color.white, 0.07f, 0.68f, 0.93f, 0.9f);
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            float yMax = 0.65f - 0.115f * i;
            optionButtons[i] = Btn(quizPanel.transform, "", 36, Blue, 0.07f, yMax - 0.1f, 0.93f, yMax, () => OnAnswer(idx), out optionTexts[i]);
        }
        quizFeedback = Txt(quizPanel.transform, "", 36, TextAnchor.MiddleCenter, Color.white, 0.07f, 0.1f, 0.93f, 0.2f);
        nextButton = Btn(quizPanel.transform, "Next", 42, Green, 0.25f, 0.02f, 0.75f, 0.09f, OnNext, out _);
    }

    // 6. COMPLETE
    void BuildResult()
    {
        resultPanel = Container("Result");
        Box(resultPanel.transform, Panel, 0, 0, 1, 1);
        var t = Txt(resultPanel.transform, "MISSION COMPLETE!", 76, TextAnchor.MiddleCenter, Gold, 0.05f, 0.8f, 0.95f, 0.92f);
        t.fontStyle = FontStyle.Bold;
        resultText = Txt(resultPanel.transform, "", 44, TextAnchor.UpperCenter, Color.white, 0.08f, 0.33f, 0.92f, 0.78f);
        Btn(resultPanel.transform, "Keep Exploring", 38, Blue, 0.06f, 0.17f, 0.48f, 0.26f, () => gm.SetStage(Stage.Interact));
        Btn(resultPanel.transform, "Play Again", 38, Green, 0.52f, 0.17f, 0.94f, 0.26f, () => gm.ResetGame());
    }

    void BuildToast()
    {
        toastBox = R("Toast", root, 0.1f, 0.82f, 0.9f, 0.89f).gameObject;
        Box(toastBox.transform, Bar, 0, 0, 1, 1, false);
        toastText = Txt(toastBox.transform, "", 36, TextAnchor.MiddleCenter, Color.white, 0.03f, 0.05f, 0.97f, 0.95f);
        toastBox.SetActive(false);
    }

    // ================= behaviour =================
    void Refresh()
    {
        var s = gm.CurrentStage;
        startPanel.SetActive(s == Stage.Start);
        preparePanel.SetActive(s == Stage.Prepare);
        hudPanel.SetActive(s == Stage.Interact);
        quizPanel.SetActive(s == Stage.Quiz);
        resultPanel.SetActive(s == Stage.Complete);
        if (s != Stage.Interact) { infoCard.SetActive(false); ItemHotspot.PopNone(); }

        progressText.text = "Discovered " + gm.FoundCount + "/" + gm.ItemCount;
        pointsText.text = "Points " + gm.Points + "  |  " + Mathf.RoundToInt(gm.Percent * 100f) + "% complete";
        progressFill.anchorMax = new Vector2(0.04f + 0.92f * gm.FoundCount / gm.ItemCount, 0.35f);
        quizBtnLabel.text = gm.QuizDone ? "Results" : (gm.AllFound ? "Quiz!" : "Quiz (locked)");

        if (s == Stage.Complete) ShowResults();
    }

    public void ShowItem(int i)
    {
        var item = GoBagData.Items[i];
        bool isNew = gm.Discover(i);
        infoTitle.text = item.name;
        infoBody.text = item.info + "\n\nTip: " + item.tip;
        infoPts.text = isNew ? "+" + GoBagData.PointsPerItem + " points! New feature discovered" : "Already discovered";
        infoCard.SetActive(true);
        ItemHotspot.PopOnly(i);      // pop the 3D item
        cardT = 0f;                  // pop the info card
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
        quizHeader.text = "Question " + (qIndex + 1) + "/" + GoBagData.Quiz.Length + "   |   Points " + gm.Points;
        quizQuestion.text = q.text;
        for (int i = 0; i < 4; i++)
        {
            optionTexts[i].text = q.options[i];
            optionButtons[i].image.color = Blue;
        }
        quizFeedback.text = "";
        nextButton.gameObject.SetActive(false);
    }

    void OnAnswer(int i)
    {
        if (answered) return;
        answered = true;
        var q = GoBagData.Quiz[qIndex];
        bool ok = i == q.answer;
        gm.AnswerQuiz(ok);
        optionButtons[q.answer].image.color = Green;
        if (!ok) optionButtons[i].image.color = new Color(0.8f, 0.2f, 0.2f);
        quizFeedback.text = (ok ? "Correct! +" + GoBagData.PointsPerAnswer + " points\n" : "Not quite.\n") + q.explain;
        quizFeedback.color = ok ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.6f, 0.6f);
        quizHeader.text = "Question " + (qIndex + 1) + "/" + GoBagData.Quiz.Length + "   |   Points " + gm.Points;
        nextButton.GetComponentInChildren<Text>().text = qIndex == GoBagData.Quiz.Length - 1 ? "See Results" : "Next Question";
        nextButton.gameObject.SetActive(true);
    }

    void OnNext()
    {
        qIndex++;
        if (qIndex >= GoBagData.Quiz.Length) gm.FinishQuiz();
        else ShowQuestion();
    }

    void ShowResults()
    {
        string s = "Score: " + gm.Points + " / " + gm.MaxPoints +
                   "\nCompletion: " + Mathf.RoundToInt(gm.Percent * 100f) + "%" +
                   "\nItems discovered: " + gm.FoundCount + "/" + gm.ItemCount +
                   "\nQuiz: " + gm.QuizCorrect + "/" + GoBagData.Quiz.Length +
                   "\n\nRank: " + gm.Rank + "\n\nAchievements";
        foreach (var a in gm.Achievements()) s += "\n* " + a;
        resultText.text = s;
    }

    void Toast(string msg) { toastText.text = msg; toastBox.SetActive(true); toastTimer = 2.5f; }

    void Update()
    {
        if (toastTimer > 0f)
        {
            toastTimer -= Time.deltaTime;
            if (toastTimer <= 0f) toastBox.SetActive(false);
        }
        if (cardT < 1f)
        {
            cardT = Mathf.Min(1f, cardT + Time.deltaTime * 3f);
            infoCard.transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, ItemHotspot.EaseOutBack(cardT));
        }
    }

}
