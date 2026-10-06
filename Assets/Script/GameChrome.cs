using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 游戏里的四角小字（分镜 #gui）：顶边进度线、左上「第一章 · 仲夏夜  1.03  七月 · 仲夏夜」、
// 对话框左边的行号 L.0103 + 小刻度 + 「03 / 14」、右下页码。夜里的背景上字换成纸色（1.4 秒渐变）。
// 数据全从 DialogueManager 和 ChapterCard 读，不用填表。界面零件在 Awake 里自己生成。
public class GameChrome : MonoBehaviour
{
    public DialogueManager dialogue;
    public ChapterCard chapterCard;
    public TMP_FontAsset font;        // 有中文的字体（MainFont）
    public string[] darkBackgrounds = { "night", "streetnight", "grassnight", "ascent_space", "earth" };

    static readonly int[] PageSkip = { 0, 2, 5, 9, 14 };
    static readonly Color Cobalt = new Color(0f, 0.278f, 0.671f);
    static readonly Color Paper = new Color(0.949f, 0.925f, 0.875f);

    RectTransform root, prog, ticksRoot;
    TextMeshProUGUI chap, scene, place, line, count, page;
    Image[] ticks = new Image[0];
    CanvasGroup group;
    int lastIndex = -2, lastN = -1, lastFx;
    float darkK;                      // 0 = 钴蓝字，1 = 纸色字
    public static float DarkK;        // 给快捷栏用
    // 每一行属于哪一段（label）、是这一段的第几句、这一段一共几句
    string[] secOf; int[] noOf; int[] nOf;

    void Awake()
    {
        if (dialogue == null) dialogue = FindObjectOfType<DialogueManager>();
        if (chapterCard == null) chapterCard = FindObjectOfType<ChapterCard>(true);
        if (FindObjectOfType<SceneCam>() == null)                   // 场景镜头（第二章飞行段）：没人放就自己建一个，不用手拖
        {
            var camGo = new GameObject("SceneCam", typeof(RectTransform));
            camGo.transform.SetParent(transform.parent, false);
            camGo.AddComponent<SceneCam>();
        }
        root = (RectTransform)transform;
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
        group = gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.alpha = 0f;

        prog = Rect("Progress", new Vector2(0, 0), new Vector2(1920, 3)); prog.GetComponent<Image>().color = Cobalt; prog.localScale = new Vector3(0, 1, 1);
        prog.anchorMin = new Vector2(0f, 1f); prog.anchorMax = new Vector2(1f, 1f); prog.pivot = new Vector2(0f, 1f);
        prog.anchoredPosition = Vector2.zero; prog.sizeDelta = new Vector2(0f, 3f);   // 顶边进度线：跟着画布宽度拉通
        chap = Text("Chapter", new Vector2(120, -32), 300, TextAlignmentOptions.TopLeft);
        scene = Text("Scene", new Vector2(0, -32), 80, TextAlignmentOptions.TopLeft);
        place = Text("Place", new Vector2(0, -32), 300, TextAlignmentOptions.TopLeft); place.alpha = 0.6f;
        line = Text("Line", new Vector2(262, -846), 118, TextAlignmentOptions.TopRight);
        count = Text("Count", new Vector2(262, -892), 118, TextAlignmentOptions.TopRight);
        page = Text("Page", new Vector2(1920 - 120 - 200, -1032), 200, TextAlignmentOptions.TopRight);
        page.rectTransform.anchorMin = page.rectTransform.anchorMax = new Vector2(1f, 1f);   // 页码贴右缘
        page.rectTransform.pivot = new Vector2(1f, 1f);
        page.rectTransform.anchoredPosition = new Vector2(-120f, -1032f);
        ticksRoot = Rect("Ticks", new Vector2(262, -874), new Vector2(118, 8)); ticksRoot.GetComponent<Image>().enabled = false;
    }

    // ───────── 宽屏适配（安卓 / iOS / Mac）：画布只往大处长、谁都不裁；背景 cover 铺满；中间那批跟着中线走 ─────────
    float fittedW = -1f;
    List<RectTransform> centerRts;
    readonly List<float> centerOrigX = new List<float>();

    void FitWide()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var crt = (RectTransform)canvas.transform;
        // 注：每帧找一次 Canvas 成本可以忽略（就两个父级），保持代码短
        float cw = crt.rect.width, ch = crt.rect.height;
        if (cw <= 1f || Mathf.Abs(cw - fittedW) < 0.5f) return;    // 还没布局好 / 没变：不动
        if (fittedW < 0f)                                          // 第一次：登记要跟着中线走的物体（只登记左侧定死的）
        {
            var scaler = GetComponentInParent<CanvasScaler>();
            if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // 画布 ≥ 设计尺寸，不裁任何东西
            centerRts = new List<RectTransform>();
            if (dialogue != null && dialogue.dialogueText != null && dialogue.dialogueText.transform.parent is RectTransform)
                AddCenter((RectTransform)dialogue.dialogueText.transform.parent);                 // 对话框
            foreach (string n in new[] { "Stage", "ChoiceBox", "ChoicePanel", "NamePlate" })
            {
                var go = GameObject.Find("Canvas/" + n);
                if (go != null) AddCenter((RectTransform)go.transform);
            }
            foreach (var rt in centerRts) centerOrigX.Add(rt.anchoredPosition.x);
        }
        fittedW = cw;
        float dx = (cw - 1920f) * 0.5f;                            // 多出来的宽度的一半 = 往右挪多少
        for (int i = 0; i < centerRts.Count; i++)
        {
            var rt = centerRts[i];
            if (rt == null) continue;
            rt.anchoredPosition = new Vector2(centerOrigX[i] + dx, rt.anchoredPosition.y);
        }
        float cover = Mathf.Max(cw / 1920f, ch / 1080f);           // 背景：cover（不拉伸，多出来的裁掉）
        RectTransform bgRt = dialogue != null && dialogue.backgroundImage != null ? dialogue.backgroundImage.rectTransform : null;
        RectTransform lineRt = dialogue != null && dialogue.backgroundLine != null ? (RectTransform)dialogue.backgroundLine.transform : null;
        foreach (var rt in new[] { bgRt, lineRt })
        {
            if (rt == null) continue;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(1920f, 1080f);
            rt.localScale = new Vector3(cover, cover, 1f);
        }
        var flash = GameObject.Find("Canvas/FlashOverlay");        // 闪白层：拉到满
        if (flash != null)
        {
            var fr = (RectTransform)flash.transform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;
        }
    }

    void AddCenter(RectTransform rt)
    {
        if (rt == null) return;
        foreach (var e in centerRts) if (e == rt || rt.IsChildOf(e)) return;    // 同一个或它的子物体：不重复登记
        if (!Mathf.Approximately(rt.anchorMin.x, 0f) || !Mathf.Approximately(rt.anchorMax.x, 0f)) return;   // 只动左边钉死的
        centerRts.Add(rt);
    }

    // 把表格切成段：label 行开一段；有台词的行才算一句
    void Index()
    {
        var L = dialogue.dialogueLines; int n = L.Count;
        secOf = new string[n]; noOf = new int[n]; nOf = new int[n];
        string sec = ""; int start = 0, no = 0;
        for (int i = 0; i <= n; i++)
        {
            bool label = i < n && L[i].flow != null && L[i].flow.StartsWith("label:");
            if (i == n || label)
            {
                for (int k = start; k < i; k++) nOf[k] = no;
                if (i == n) break;
                sec = L[i].flow.Substring(6).Trim(); start = i; no = 0;
            }
            secOf[i] = sec;
            if (!string.IsNullOrEmpty(L[i].text) && (L[i].flow == null || !L[i].flow.StartsWith("option:"))) no++;
            noOf[i] = no;
        }
    }

    void Update()
    {
        FitWide();   // 画布尺寸变了（宽屏手机 / 窗口拉伸）就重新适配一次
        if (secOf == null) { if (dialogue.dialogueLines == null || dialogue.dialogueLines.Count == 0) return; Index(); }   // 等表格读进来
        int idx = dialogue.CurrentIndex;
        group.alpha = Mathf.MoveTowards(group.alpha, idx >= 0 ? 1f : 0f, Time.deltaTime / 0.24f);
        if (idx >= 0 && idx < secOf.Length && (idx != lastIndex || PageFx.Level != lastFx)) { lastIndex = idx; lastFx = PageFx.Level; Fill(idx); }

        // 夜里换成纸色字
        string bg = dialogue.backgroundImage != null && dialogue.backgroundImage.sprite != null ? dialogue.backgroundImage.sprite.name.Replace("_base", "") : "";
        bool dark = System.Array.IndexOf(darkBackgrounds, bg) >= 0;
        darkK = Mathf.MoveTowards(darkK, dark ? 1f : 0f, Time.deltaTime / 1.4f);
        DarkK = darkK;
        Color c = Color.Lerp(Cobalt, Paper, darkK);
        foreach (var t in new[] { chap, scene, line, count, page }) t.color = new Color(c.r, c.g, c.b, t.color.a);
        place.color = new Color(c.r, c.g, c.b, 0.6f); count.alpha = 0.7f;
        prog.GetComponent<Image>().color = c;
        int lit = idx >= 0 && idx < noOf.Length ? noOf[idx] : 0;
        for (int i = 0; i < ticks.Length; i++) { ticks[i].color = i < lit ? c : new Color(c.r, c.g, c.b, 0f); ticks[i].transform.GetChild(0).GetComponent<Image>().color = new Color(c.r, c.g, c.b, 0.45f); }
    }

    void Fill(int idx)
    {
        string sec = secOf[idx]; int no = noOf[idx], N = nOf[idx];
        int ch = sec == "第一章" ? 1 : sec == "第二章" ? 2 : sec == "第三章" ? 3 : sec == "第四章" ? 4 : 5;   // A / B 属于第五章
        var info = chapterCard != null && ch - 1 < chapterCard.chapters.Length ? chapterCard.chapters[ch - 1] : null;
        chap.text = info != null ? "第" + info.numeral + "章 · " + info.title : sec;
        scene.text = ch + "." + no.ToString("00");
        place.text = info != null ? info.place : "";
        LayoutHead();
        line.text = "L." + ch.ToString("00") + no.ToString("00");
        count.text = no.ToString("00") + " / " + N.ToString("00");
        int pg = 0; if (info != null) int.TryParse(info.page, out pg);
        page.text = (pg + no + PageSkip[PageFx.Level]).ToString("000");   // 第 8 幕：页码开始对不上
        prog.localScale = new Vector3(N > 0 ? (float)no / N : 0f, 1f, 1f);
        if (N != lastN) { lastN = N; BuildTicks(N); }
    }

    // 左上三段字之间空 24
    void LayoutHead()
    {
        float x = 120f;
        foreach (var t in new[] { chap, scene, place })
        {
            var rt = t.rectTransform; rt.anchoredPosition = new Vector2(x, -32);
            x += t.GetPreferredValues(t.text).x + 24f;
        }
    }

    void BuildTicks(int N)
    {
        foreach (Transform c in ticksRoot) Destroy(c.gameObject);
        ticks = new Image[N];
        if (N <= 0) return;
        float w = (118f - 2f * (N - 1)) / N;
        for (int i = 0; i < N; i++)
        {
            var go = new GameObject("Tick", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(ticksRoot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(i * (w + 2f), 0f); rt.sizeDelta = new Vector2(w, 8f);
            ticks[i] = go.GetComponent<Image>(); ticks[i].raycastTarget = false;
            // 没亮的格子：一圈细框
            var frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            var fr = frame.GetComponent<RectTransform>(); fr.SetParent(rt, false);
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = Vector2.zero; fr.offsetMax = Vector2.zero;
            var fi = frame.GetComponent<Image>(); fi.raycastTarget = false; fi.sprite = FrameSprite(); fi.type = Image.Type.Sliced;
        }
    }

    static Sprite frameSprite;
    static Sprite FrameSprite()      // 3×3 的细框贴图，九宫格拉伸
    {
        if (frameSprite != null) return frameSprite;
        var tex = new Texture2D(3, 3) { filterMode = FilterMode.Point };
        for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++) tex.SetPixel(x, y, x == 1 && y == 1 ? Color.clear : Color.white);
        tex.Apply();
        frameSprite = Sprite.Create(tex, new Rect(0, 0, 3, 3), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(1, 1, 1, 1));
        return frameSprite;
    }

    RectTransform Rect(string n, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(root, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        go.GetComponent<Image>().raycastTarget = false;
        return rt;
    }

    TextMeshProUGUI Text(string n, Vector2 pos, float w, TextAlignmentOptions al)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(root, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(w, 16f);
        var t = go.GetComponent<TextMeshProUGUI>(); if (font != null) t.font = font;
        t.fontSize = 12f; t.characterSpacing = 9f; t.color = Cobalt; t.alignment = al;
        t.enableWordWrapping = false; t.raycastTarget = false;
        return t;
    }
}
