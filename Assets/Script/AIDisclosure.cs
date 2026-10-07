using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// N°00 · 创作声明（分镜 AI 说明的「飞行记录」那一页）：启动游戏后最先看到。
// 七项 AI 参与比例，每一项都要亲手从坡顶放飞一架滑翔机：拖动瞄准（往右越远 = AI 占比越高），落在刻度上才算数；
// 也可以「等一阵风」一次全部吹到位。七次都落地后出现「开始阅读」，接开场视频（OpeningPlayer 等它结束）。
// 画面（天空、坡、太阳、小孩、滑翔机）是分镜导出的图（Sprites/Fly/UI/ai_*），其余全部在这里生成。
// 坐标照分镜：1920×1080、左上原点；整页放在居中的 1920×1080 舞台里，宽屏两边是纸色。
public class AIDisclosure : MonoBehaviour
{
    public static bool Shown;                       // 这次启动已经看过了
    public static bool Active { get; private set; }
    public static bool ForceShow;                  // 标题左下「N°00 · AI 说明」再打开一次

    public TMP_FontAsset uiFont, textFont, handFont;   // MainFont / WenKai / KuaiLe

    static readonly Color Cobalt = new Color(0f, 0.278f, 0.671f);
    static readonly Color Paper = new Color(0.949f, 0.925f, 0.875f);
    static readonly Color Sun = new Color(0.949f, 0.71f, 0.122f);
    static readonly Color Ink = new Color(0.212f, 0.325f, 0.604f);

    class Row { public string cn, en, rg, h, a; public int ai; public bool done; public TextMeshProUGUI val; public Image mark; }
    readonly Row[] rows = {
        new Row { cn = "故事与文本", en = "STORY & TEXT", ai = 15, rg = "10–20%", h = "故事、人物、梦境与两个结局；全部初稿与定稿。", a = "结构上的意见；逐句校对与润色。" },
        new Row { cn = "立绘", en = "CHARACTER ART", ai = 15, rg = "10–20%", h = "全部立绘手绘（线稿与上色）。", a = "转成蜡笔与水彩质感，合进画面。" },
        new Row { cn = "背景、界面与画面", en = "PICTURES & UI", ai = 90, rg = "85–95%", h = "美术方向；逐轮审阅与修改意见。", a = "以代码绘制全部背景、界面与插图。" },
        new Row { cn = "动态与演出", en = "MOTION & STAGING", ai = 90, rg = "85–95%", h = "镜头与演出的构思。", a = "动画、转场与镜头的实现。" },
        new Row { cn = "程序", en = "PROGRAMMING", ai = 97, rg = "95–100%", h = "测试与反馈；Unity 框架的一部分。", a = "几乎全部代码。" },
        new Row { cn = "音乐与音效", en = "MUSIC & SOUND", ai = 100, rg = "100%", h = "风格与情绪方向。", a = "全部音乐与音效，以程序合成。" },
        new Row { cn = "整体", en = "THE WHOLE WORK", ai = 60, rg = "55–65%", h = "创意、方向、取舍与最终审定。", a = "各部分的执行与实现。" },
    };

    // 飞行记录的几何（分镜 LG）
    const float RX0 = 960f, RX1 = 1640f, G = 904f, SNAP = 3f;
    static readonly Vector2 LP = new Vector2(880f, 828f);

    RectTransform stage; CanvasGroup group;
    RectTransform glider; Sprite[] gliderFrames;
    TextMeshProUGUI detail, hint, readout, wholeLine, finishText, finishCap;
    RectTransform targetRoot; Button windBtn, goBtn; CanvasGroup goGroup;
    readonly List<Image> preview = new List<Image>();
    Sprite dot; AudioSource se;
    int cur; float d; bool snapped, aiming, flying, auto, finished, intro = true;
    TextMeshProUGUI stat, repoNote; CanvasGroup gliderDrop, guide; Image finger, fingerRing; readonly List<Image> guideArc = new List<Image>(); bool touched; float guideT;
    public const string REPO_URL = "https://github.com/IEndndI/Fly";   // GitHub 仓库地址（Application.OpenURL）
    class Anim { public RectTransform rt; public CanvasGroup g; public float delay; public int kind; public Vector2 home; }
    readonly List<Anim> anims = new List<Anim>();

    void Awake()
    {
        if (Shown && !ForceShow) { Destroy(gameObject); return; }
        ForceShow = false;
        if (uiFont == null) { var ts = FindObjectOfType<TitleScreen>(); if (ts != null) { uiFont = ts.uiFont; textFont = ts.textFont; handFont = ts.labelFont; } }   // 从标题再打开时：字体跟标题拿
        Shown = true; Active = true;
        var r = (RectTransform)transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; r.SetAsLastSibling();
        gameObject.AddComponent<Bleed>();                                                     // 纸色底伸到屏幕边（宽屏 / 平板）
        var bgImg = gameObject.AddComponent<Image>(); bgImg.color = Paper;                    // 挡住后面的标题，也接住点击
        group = gameObject.AddComponent<CanvasGroup>();
        se = gameObject.AddComponent<AudioSource>(); se.playOnAwake = false; se.volume = SaveSystem.Sfx;
        dot = MakeDot();

        stage = Rect("Stage", r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f));
        stage.pivot = new Vector2(0.5f, 0.5f);

        // ── 右边的画：天空（洇开）+ 蜡笔（抖） ──
        var pic = Img("Sky", 0, 0, 1920, 1080, Color.white); pic.sprite = Resources.Load<Sprite>("Sprites/Fly/UI/ai_bg"); In(pic, 0.2f, 4);
        var line = Img("Crayon", 0, 0, 1920, 1080, Color.white);
        line.gameObject.AddComponent<BoilSprite>().SetFrames(Load3("ai_line_v")); In(line, 0.9f, 3);
        gliderFrames = Load3("ai_glider_v");
        // ── 瑞士平面：12 栏网格（栏宽 118、栏距 24、页边 120），一切都对在栏线上 ──
        for (int c = 0; c < 12; c++)
        {
            float x0 = 120 + c * 142;
            In(Img("col", x0, 80, 1, 920, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.10f)), 0.02f * c, 2);
            In(Img("col", x0 + 118, 80, 1, 920, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.10f)), 0.02f * c + 0.01f, 2);
        }
        In(Img("rowT", 0, 80, 1920, 1, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.22f)), 0f, 1);
        In(Img("rowB", 0, 1000, 1920, 1, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.22f)), 0.1f, 1);
        foreach (var p in new[] { new Vector2(56, 56), new Vector2(1864, 56), new Vector2(56, 1024), new Vector2(1864, 1024) }) Reg(p.x, p.y, 0.1f);
        foreach (var p in new[] { new Vector2(120, 80), new Vector2(1800, 80), new Vector2(120, 1000), new Vector2(1800, 1000) }) Crop(p.x, p.y, 0.15f);

        // 空心大数字（页码 00），压在天上
        var big = Txt("00", uiFont, 420f, 1200, 130, 600, 440, FontStyles.Bold, TextAlignmentOptions.TopRight);
        big.faceColor = new Color32(0, 0, 0, 0); big.outlineWidth = 0.035f; big.outlineColor = new Color32(0, 71, 171, 110); In(big, 0.5f, 0);

        // ── 页眉 / 页脚 ──
        In(Cap("N°00 · 读之前 · BEFORE YOU READ", 120, 32, 560), 0.2f, 0);
        In(Cap("FLIGHT LOG / 000", 688, 32, 260), 0.25f, 0);
        In(Cap("ALTITUDE / 12 M · 坡顶 HILLTOP", 972, 32, 400), 0.3f, 0);
        In(Cap("飞 / FLY — 创作声明 · STATEMENT", 1200, 32, 600, TextAlignmentOptions.TopRight), 0.35f, 0);
        In(Cap("ENDND × CLAUDE OPUS 5.5 — 2026.10", 120, 1032, 560), 0.4f, 0);
        In(Cap("P. 000", 1500, 1032, 300, TextAlignmentOptions.TopRight), 0.45f, 0);
        // 印刷色标（钴蓝 / 浅钴蓝 / 黄 / 纸 / 墨）
        float sx = 688;
        foreach (var c in new[] { Cobalt, new Color(0.35f, 0.5f, 0.78f), new Color(0.6f, 0.7f, 0.88f), Sun, Paper, Ink })
        {
            var sw = Img("swatch", sx, 1030, 24, 14, c);
            if (c == Paper) sw.gameObject.AddComponent<Outline>().effectColor = Cobalt;
            In(sw, 0.5f + (sx - 688) / 600f, 3); sx += 28;
        }

        // ── 左栏（第 1–5 栏，120–806）──
        In(Cap("N°00 — 创作声明 · STATEMENT", 120, 112, 686), 0.3f, 0);
        In(Txt("创作声明：\n此项目由 <color=#0047AB>AI</color> 辅助完成", uiFont, 46f, 120, 136, 686, 124, FontStyles.Bold), 0.4f, 0);
        In(Img("bar", 120, 268, 686, 5, Cobalt), 0.65f, 1);                                  // 粗钴蓝条：版式的骨头
        var body = Txt("此故事出自于作者的梦。在这个作品中，故事与文本、立绘主要由人类创作，画面、动画程序、音乐由 Claude Opus 5.5 完成。",
            textFont, 18f, 120, 288, 686, 64); body.enableWordWrapping = true; body.lineSpacing = 14f; In(body, 0.75f, 0);
        // 大数字：整体 AI ≈ 60%（入场时从 0 数上来）
        stat = Txt("0", uiFont, 112f, 112, 352, 190, 120, FontStyles.Bold, TextAlignmentOptions.TopRight); In(stat, 0.9f, 0);
        In(Txt("%", uiFont, 44f, 304, 372, 60, 60, FontStyles.Bold), 0.95f, 0);
        In(Cap("AI 参与 · OVERALL", 376, 372, 430), 1.0f, 0);
        In(Txt("整体而言，AI 参与约 60%。", textFont, 20f, 376, 394, 430, 30, FontStyles.Bold), 1.05f, 0);
        In(Cap("区间 55–65% · 按各部分分量加权", 376, 430, 430), 1.1f, 0);
        wholeLine = Cap("✓ 七项均已核对 · ALL CONFIRMED", 376, 452, 430); wholeLine.alpha = 0f;
        // 钴蓝色块压纸色字：给不接受 AI 的读者的提醒
        In(Img("band", 120, 484, 686, 40, Cobalt), 1.2f, 1);
        var warn = Txt("若您反感 AI 创作，此作品可能并不适合你。", textFont, 18f, 136, 492, 660, 26); warn.color = Paper; In(warn, 1.35f, 0);

        // 飞行记录表
        In(Cap("飞行记录 · FLIGHT LOG", 120, 572, 400), 1.5f, 0);
        In(Cap("区间 · RANGE          AI", 120, 572, 686, TextAlignmentOptions.TopRight), 1.5f, 0);
        In(Img("rule", 120, 592, 686, 1, Cobalt), 1.5f, 1);
        for (int i = 0; i < rows.Length; i++)
        {
            float y = 596 + i * 38, dl = 1.55f + i * 0.06f;
            var n = Txt("0" + (i + 1), uiFont, 12f, 120, y + 12, 40, 18); n.alpha = 0.7f; In(n, dl, 5);
            In(Txt(rows[i].cn + "  <size=10><alpha=#99>" + rows[i].en + "<alpha=#FF></size>", uiFont, 18f, 160, y + 7, 420, 26), dl, 5);
            var rg = Txt(rows[i].rg, uiFont, 12f, 560, y + 12, 120, 18, FontStyles.Normal, TextAlignmentOptions.TopRight); rg.alpha = 0.6f; In(rg, dl, 5);
            rows[i].val = Txt("—", uiFont, 19f, 690, y + 7, 116, 26, FontStyles.Bold, TextAlignmentOptions.TopRight); rows[i].val.alpha = 0.25f; In(rows[i].val, dl, 5);
            rows[i].mark = Img("cur", 104, y + 8, 4, 24, Sun); rows[i].mark.enabled = false;
            In(Img("rule", 120, y + 38, 686, 1, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.22f)), dl, 1);
        }
        detail = Txt("", textFont, 14f, 120, 868, 686, 60); detail.enableWordWrapping = true; detail.lineSpacing = 10f; In(detail, 2.0f, 0);

        windBtn = MakeButton("或者，等一阵风　<size=11><alpha=#AA>LET THE WIND<alpha=#FF></size>", 120, 944, 330, 44, false, Wind); In(windBtn.targetGraphic, 2.05f, 0);
        var gh = MakeButton("GitHub · 全部开源 ↗", 476, 944, 330, 44, true, OpenRepo); In(gh.targetGraphic, 2.1f, 0);
        repoNote = Cap("", 476, 994, 400); repoNote.alpha = 0f;
        goBtn = MakeButton("开始阅读　<size=11>BEGIN →</size>", 1114, 384, 280, 48, true, Finish);
        goGroup = goBtn.gameObject.AddComponent<CanvasGroup>(); goGroup.alpha = 0f; goGroup.interactable = goGroup.blocksRaycasts = false;

        // 刻度尺（AI 参与比例 = 飞多远）
        for (int v = 0; v <= 100; v += 5)
        {
            float x = X(v); bool maj = v % 25 == 0;
            In(Img("tick", x, G + 4, 1.2f, maj ? 14 : 7, new Color(Cobalt.r, Cobalt.g, Cobalt.b, maj ? 0.85f : 0.45f)), 1.2f + v * 0.004f, 2);
            if (maj) { var tl = Txt(v.ToString(), uiFont, 13f, x - 30, G + 24, 60, 18, FontStyles.Normal, TextAlignmentOptions.Top); tl.alpha = 0.75f; In(tl, 1.3f + v * 0.004f, 0); }
        }
        In(Cap("AI 参与比例 → / DISTANCE", RX1 - 400, G + 52, 400, TextAlignmentOptions.TopRight), 1.6f, 0);
        In(Cap("坡顶 / HILLTOP", LP.x - 40, G + 52, 200), 1.6f, 0);

        targetRoot = Rect("Target", stage, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
        for (int i = 0; i < 40; i++) { var p = Img("arc", 0, 0, 5, 5, Cobalt); p.sprite = dot; p.enabled = false; preview.Add(p); }
        readout = Txt("", uiFont, 16f, 0, 0, 80, 22, FontStyles.Bold); readout.alpha = 0f;
        hint = Txt("", textFont, 26f, 0, 0, 400, 36); hint.alpha = 0f;
        var gimg = Img("Glider", LP.x, LP.y, 112, 44, Color.white);
        glider = gimg.rectTransform; glider.pivot = new Vector2(0.5f, 1f - 120f / 220f); glider.anchoredPosition = new Vector2(LP.x, -LP.y);
        gimg.gameObject.AddComponent<BoilSprite>().SetFrames(gliderFrames);
        gliderDrop = gimg.gameObject.AddComponent<CanvasGroup>(); gliderDrop.alpha = 0f;
        // 右边天空里的操作提示：一句话 + 箭头 + 手势（按下 → 往右拖 → 松手），一开始拖就淡出
        guide = Rect("Guide", stage, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero).gameObject.AddComponent<CanvasGroup>();
        guide.alpha = 0f; guide.blocksRaycasts = false;
        var gt = Txt("请滑动屏幕，瞄准掷出的滑翔机\n到它该落下的刻度，松手，放飞。", textFont, 24f, 1000, 600, 640, 80); gt.lineSpacing = 12f; Child((RectTransform)guide.transform, gt);
        var gc = Cap("DRAG → AIM → RELEASE", 1000, 676, 400); Child((RectTransform)guide.transform, gc);
        var shaft = Img("arrow", 1000, 716, 360, 3, Cobalt); Child((RectTransform)guide.transform, shaft);
        foreach (int sgn in new[] { 1, -1 })
        {
            var head = Img("head", 1360, 717, 18, 3, Cobalt); head.rectTransform.pivot = new Vector2(1f, 0.5f);
            head.rectTransform.anchoredPosition = new Vector2(1362, -717.5f); head.rectTransform.localEulerAngles = new Vector3(0, 0, 35 * sgn);
            Child((RectTransform)guide.transform, head);
        }
        finger = Img("finger", 0, 0, 30, 30, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.85f)); finger.sprite = dot; finger.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        Child((RectTransform)guide.transform, finger);
        fingerRing = Img("fingerRing", 0, 0, 30, 30, Cobalt); fingerRing.sprite = RingSprite(); fingerRing.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        Child((RectTransform)guide.transform, fingerRing);
        for (int i = 0; i < 24; i++) { var gp = Img("gdot", 0, 0, 4, 4, Cobalt); gp.sprite = dot; gp.rectTransform.pivot = new Vector2(0.5f, 0.5f); Child((RectTransform)guide.transform, gp); guideArc.Add(gp); }

        finishText = Txt("七次飞行，均已落地。", handFont, 46f, 1110, 270, 700, 60); finishText.color = Ink; finishText.alpha = 0f;
        finishCap = Cap("ALL FLIGHTS LANDED / 7 OF 7", 1114, 340, 500); finishCap.alpha = 0f;

        StartCoroutine(Intro());
    }

    void Start() { transform.SetAsLastSibling(); }   // 压在开场视频和标题上面

    // ───────── 入场：栏线画出来 → 天空洇开 → 文字错峰浮起 → 大数字数上来 → 色块刷过 → 表格一行行进 → 滑翔机落到坡顶 ─────────
    // kind：0 浮起淡入　1 横向画出　2 纵向画出　3 淡入　4 洇开（淡入 + 微缩）　5 从左滑入
    void In(Graphic gr, float delay, int kind) { if (gr != null) In(gr.rectTransform, delay, kind); }
    void In(RectTransform rt, float delay, int kind)
    {
        var g = rt.GetComponent<CanvasGroup>(); if (g == null) g = rt.gameObject.AddComponent<CanvasGroup>();
        g.alpha = 0f;
        var a = new Anim { rt = rt, g = g, delay = delay, kind = kind, home = rt.anchoredPosition };
        if (kind == 1) { rt.pivot = new Vector2(0f, rt.pivot.y); rt.anchoredPosition = a.home; rt.localScale = new Vector3(0f, 1f, 1f); }
        if (kind == 2) { rt.localScale = new Vector3(1f, 0f, 1f); }
        anims.Add(a);
    }

    IEnumerator Intro()
    {
        Play("sfx_page");
        float end = 0f; foreach (var a in anims) end = Mathf.Max(end, a.delay + 0.9f);
        bool counted = false;
        for (float t = 0f; t < end; t += Time.deltaTime)
        {
            foreach (var a in anims)
            {
                float u = Mathf.Clamp01((t - a.delay) / (a.kind == 4 ? 1.4f : 0.7f)), e = 1f - Mathf.Pow(1f - u, 3f);
                a.g.alpha = a.kind == 1 || a.kind == 2 ? (u > 0f ? 1f : 0f) : e;
                if (a.kind == 0) a.rt.anchoredPosition = a.home + new Vector2(0f, -14f * (1f - e));
                if (a.kind == 5) a.rt.anchoredPosition = a.home + new Vector2(-24f * (1f - e), 0f);
                if (a.kind == 1) a.rt.localScale = new Vector3(e, 1f, 1f);
                if (a.kind == 2) a.rt.localScale = new Vector3(1f, e, 1f);
                if (a.kind == 4) a.rt.localScale = Vector3.one * (1.03f - 0.03f * e);
            }
            float su = Mathf.Clamp01((t - 1.0f) / 0.9f);                                     // 60 数上来
            stat.text = Mathf.RoundToInt(60f * (1f - Mathf.Pow(1f - su, 3f))).ToString();
            if (su >= 1f && !counted) { counted = true; Play("sfx_stamp"); }
            gliderDrop.alpha = Mathf.Clamp01((t - 1.7f) / 0.4f);                             // 滑翔机落到坡顶
            if (t > 1.7f) { float g = Mathf.Clamp01((t - 1.7f) / 0.5f); PlaceGlider(LP + new Vector2(0f, -40f * (1f - (1f - Mathf.Pow(1f - g, 3f)))), -0.18f); }
            yield return null;
        }
        foreach (var a in anims) { a.g.alpha = 1f; a.rt.localScale = Vector3.one; if (a.kind == 0 || a.kind == 5) a.rt.anchoredPosition = a.home; }
        stat.text = "60"; gliderDrop.alpha = 1f;
        intro = false;
        Setup(0);
    }

    void OpenRepo()
    {
        if (!string.IsNullOrEmpty(REPO_URL)) { Application.OpenURL(REPO_URL); return; }
        repoNote.text = "仓库地址还没公开，正式发布时补上 · COMING WITH THE RELEASE"; StartCoroutine(FadeNote());
    }
    IEnumerator FadeNote()
    {
        for (float t = 0f; t < 2.4f; t += Time.deltaTime) { repoNote.alpha = t < 0.2f ? t / 0.2f : 1f - Mathf.Clamp01((t - 1.8f) / 0.6f); yield return null; }
        repoNote.alpha = 0f;
    }

    // 套准十字（印刷对位标记）与裁切线
    void Reg(float x, float y, float delay)
    {
        var c = Img("reg", x - 9, y - 9, 18, 18, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.7f)); c.sprite = RingSprite(); In(c, delay, 3);
        In(Img("regH", x - 16, y, 32, 1, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.7f)), delay, 3);
        In(Img("regV", x, y - 16, 1, 32, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.7f)), delay, 3);
    }
    void Crop(float x, float y, float delay)
    {
        float dx = x < 960 ? -1 : 1, dy = y < 540 ? -1 : 1;
        In(Img("cropH", dx < 0 ? x - 36 : x + 14, y, 22, 1, Cobalt), delay, 3);
        In(Img("cropV", x, dy < 0 ? y - 36 : y + 14, 1, 22, Cobalt), delay, 3);
    }
    static Sprite ring;
    static Sprite RingSprite()
    {
        if (ring != null) return ring;
        int n = 64; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false); float c = (n - 1) * 0.5f, r = n * 0.5f - 2f;
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float d = Mathf.Abs(Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) - r); tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(2.2f - d))); }
        tex.Apply(); return ring = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    // ───────── 一项一项来 ─────────
    void Setup(int k)
    {
        cur = k; d = 0; snapped = false;
        foreach (Transform c in targetRoot) Destroy(c.gameObject);
        float x = X(rows[k].ai);
        for (float y = G - 80; y < G - 34; y += 6) Child(targetRoot, Img("t", x, y, 1, 2.5f, Cobalt));
        var a = Img("x1", x, G - 18, 22, 2.4f, Cobalt); a.rectTransform.pivot = new Vector2(0.5f, 0.5f); a.rectTransform.anchoredPosition = new Vector2(x, -(G - 18)); a.rectTransform.localEulerAngles = new Vector3(0, 0, 45); Child(targetRoot, a);
        var b = Img("x2", x, G - 18, 22, 2.4f, Cobalt); b.rectTransform.pivot = new Vector2(0.5f, 0.5f); b.rectTransform.anchoredPosition = new Vector2(x, -(G - 18)); b.rectTransform.localEulerAngles = new Vector3(0, 0, -45); Child(targetRoot, b);
        var lab = Txt("0" + (k + 1), uiFont, 13f, x - 30, G - 104, 60, 18, FontStyles.Bold, TextAlignmentOptions.Top); Child(targetRoot, lab);
        for (int i = 0; i < rows.Length; i++) rows[i].mark.enabled = i == k;
        detail.text = "<b>0" + (k + 1) + " — " + rows[k].cn + "</b>\n<b>作者</b>　" + rows[k].h + "\n<b>AI</b>　　" + rows[k].a;
        PlaceGlider(LP, -0.18f); glider.gameObject.SetActive(true);
        Preview();
    }

    void Update()
    {
        AnimateGuide();
        if (intro || finished || flying || auto) return;
        if (Input.GetMouseButtonDown(0) && InSky(out Vector2 p)) { touched = true; aiming = true; SetD(p.x); Play("sfx_tick"); }
        else if (aiming && Input.GetMouseButton(0)) { Vector2 q = Mouse(); SetD(q.x); }
        else if (aiming && Input.GetMouseButtonUp(0)) { aiming = false; Release(); }
        if (Input.GetKeyDown(KeyCode.RightArrow)) SetDValue(d + (Input.GetKey(KeyCode.LeftShift) ? 5 : 1));
        if (Input.GetKeyDown(KeyCode.LeftArrow)) SetDValue(d - (Input.GetKey(KeyCode.LeftShift) ? 5 : 1));
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) Release();
    }

    void SetD(float stageX) => SetDValue((stageX - RX0) / (RX1 - RX0) * 100f);
    void SetDValue(float v)
    {
        bool was = snapped; d = Mathf.Clamp(v, 0f, 100f);
        snapped = Mathf.Abs(d - rows[cur].ai) <= SNAP; if (snapped) d = rows[cur].ai;
        if (snapped && !was) Play("sfx_tick");
        Preview();
    }

    // 瞄准时的虚线弧（落准了变实一点）
    void Preview()
    {
        bool on = d > 1f;
        for (int i = 0; i < preview.Count; i++)
        {
            preview[i].enabled = on;
            if (!on) continue;
            Vector2 p = Bez(d, cur, i / (float)(preview.Count - 1));
            preview[i].rectTransform.anchoredPosition = new Vector2(p.x, -p.y);
            preview[i].color = new Color(Cobalt.r, Cobalt.g, Cobalt.b, snapped ? 0.85f : 0.45f);
        }
        Vector2 end = Bez(d, cur, 1f);
        readout.text = Mathf.RoundToInt(d) + "%"; readout.alpha = on ? 1f : 0f;
        readout.rectTransform.anchoredPosition = new Vector2(Mathf.Min(end.x + 14, 1620), -(G - 36 - 22));
        PlaceGlider(LP, on ? Tan(d, cur, 0f) * 0.5f : -0.18f);
    }

    void Release()
    {
        if (finished || flying || auto) return;
        if (d < 2f) { d = 0; Preview(); return; }
        StartCoroutine(Fly(cur, d, snapped));
    }

    IEnumerator Fly(int k, float dd, bool hit)
    {
        flying = true; foreach (var p in preview) p.enabled = false; readout.alpha = 0f;
        Play("sfx_fly"); Play("sfx_whoosh");
        float dur = 1.1f + dd * 0.012f; int laid = 0;
        var mine = new List<Image>();
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float u = t / dur, e = u < 0.5f ? 2f * u * u : 1f - Mathf.Pow(-2f * u + 2f, 2f) / 2f;
            Vector2 p = Bez(dd, k, e); PlaceGlider(p, Tan(dd, k, e) * 0.8f);
            while (laid < Mathf.FloorToInt(e * 60f))                                       // 黄蜡笔轨迹一点点画出来
            {
                Vector2 q = Bez(dd, k, laid / 60f); var im = Img("trail", q.x, q.y, 7, 7, Sun); im.sprite = dot;
                im.rectTransform.pivot = new Vector2(0.5f, 0.5f); im.rectTransform.anchoredPosition = new Vector2(q.x, -q.y);
                mine.Add(im); laid++;
            }
            yield return null;
        }
        if (hit) Landed(k, mine);
        else
        {
            Play("sfx_whisper");
            Vector2 end = Bez(dd, k, 1f);
            hint.text = dd < rows[k].ai ? "近了一点。再试一次。" : "远了一点。再试一次。";
            hint.rectTransform.anchoredPosition = new Vector2(Mathf.Min(end.x - 60, 1440), -(G - 150));
            for (float t = 0f; t < 1.3f; t += Time.deltaTime)
            {
                hint.alpha = t < 0.25f ? t / 0.25f : 1f - Mathf.Clamp01((t - 0.9f) / 0.4f);
                foreach (var im in mine) im.color = new Color(Sun.r, Sun.g, Sun.b, 1f - t / 1.3f);
                yield return null;
            }
            foreach (var im in mine) Destroy(im.gameObject);
            hint.alpha = 0f; d = 0; snapped = false; Preview();
        }
        flying = false;
    }

    void Landed(int k, List<Image> mine)
    {
        Play("sfx_land"); Play("sfx_stamp");
        rows[k].done = true; rows[k].val.text = rows[k].ai + "%"; rows[k].val.alpha = 1f;
        foreach (var im in mine) { im.color = new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.32f); im.rectTransform.sizeDelta = new Vector2(3.5f, 3.5f); }   // 飞过的路线留下淡淡的铅笔线
        float x = X(rows[k].ai);
        var splash = Img("splash", x, G - 6, 44, 18, new Color(Sun.r, Sun.g, Sun.b, 0.8f)); splash.sprite = dot;
        splash.rectTransform.pivot = new Vector2(0.5f, 0.5f); splash.rectTransform.anchoredPosition = new Vector2(x, -(G - 6));
        var parked = Img("parked", x, G - 20, 62, 24, Color.white); parked.rectTransform.pivot = glider.pivot;
        parked.rectTransform.anchoredPosition = new Vector2(x, -(G - 20)); parked.rectTransform.localEulerAngles = new Vector3(0, 0, -3);
        parked.gameObject.AddComponent<BoilSprite>().SetFrames(gliderFrames);
        int same = 0; for (int i = 0; i < k; i++) if (Mathf.Abs(rows[i].ai - rows[k].ai) < 12) same++;
        bool edge = x > 1560;
        var lab = Txt("0" + (k + 1) + " · " + rows[k].ai + "%", uiFont, 11f, edge ? x - 118 : x + 16, G - 46 - same * 18, 100, 16, FontStyles.Bold, edge ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft);
        lab.characterSpacing = 4f;
        glider.gameObject.SetActive(false);
        int next = System.Array.FindIndex(rows, rr => !rr.done);
        if (next < 0) StartCoroutine(AllLanded());
        else StartCoroutine(After(auto ? 0.35f : 0.8f, () => Setup(next)));
    }

    IEnumerator After(float s, System.Action a) { yield return new WaitForSeconds(s); a(); }

    void Wind()
    {
        if (intro || auto || flying || finished) return;
        StartCoroutine(WindRun());
    }

    IEnumerator WindRun()
    {
        touched = true; auto = true; windBtn.interactable = false; Play("sfx_swoosh");
        int k;
        while ((k = System.Array.FindIndex(rows, rr => !rr.done)) >= 0)
        {
            yield return new WaitForSeconds(0.5f);
            if (cur != k) Setup(k);
            d = rows[k].ai; snapped = true; Preview(); Play("sfx_tick");
            yield return new WaitForSeconds(0.3f);
            yield return StartCoroutine(Fly(k, d, true));
            yield return new WaitForSeconds(0.45f);
        }
        auto = false;
    }

    IEnumerator AllLanded()
    {
        finished = true; windBtn.interactable = false;
        foreach (var r in rows) r.mark.enabled = false;
        foreach (Transform c in targetRoot) Destroy(c.gameObject);
        detail.text = "<b>07 — 整体</b>\n<b>作者</b>　" + rows[6].h + "\n<b>AI</b>　　" + rows[6].a;
        Play("sfx_chime"); Play("sfx_swoosh");
        for (float t = 0f; t < 1.6f; t += Time.deltaTime)
        {
            float u = Mathf.Clamp01(t / 1.2f);
            finishText.alpha = u; finishCap.alpha = Mathf.Clamp01((t - 0.6f) / 0.8f); wholeLine.alpha = Mathf.Clamp01((t - 0.3f) / 0.8f);
            finishText.maxVisibleCharacters = Mathf.CeilToInt(u * 10);                 // 一个字一个字写出来
            goGroup.alpha = Mathf.Clamp01((t - 0.8f) / 0.6f);
            yield return null;
        }
        goGroup.interactable = goGroup.blocksRaycasts = true;
    }

    void Finish() { if (finished) StartCoroutine(Leave()); }

    IEnumerator Leave()
    {
        goGroup.interactable = false; Play("sfx_page");
        for (float t = 0f; t < 0.6f; t += Time.deltaTime) { group.alpha = 1f - t / 0.6f; yield return null; }
        Active = false;
        Destroy(gameObject);
    }

    // 手势：从坡顶按下（0–0.3s），往右拖到 25% 刻度上方、虚线弧跟着长出来（0.3–1.5s），松手——圆点放大成一圈涟漪（1.5–2.1s），停一下再来
    void AnimateGuide()
    {
        if (guide == null) return;
        float want = intro || touched || finished ? 0f : 1f;
        guide.alpha = Mathf.MoveTowards(guide.alpha, want, Time.deltaTime / (want > 0f ? 0.6f : 0.25f));
        if (guide.alpha <= 0f) return;
        guideT = (guideT + Time.deltaTime) % 2.8f;
        float t = guideT, drag = Mathf.Clamp01((t - 0.3f) / 1.2f), e = drag < 0.5f ? 2f * drag * drag : 1f - Mathf.Pow(-2f * drag + 2f, 2f) / 2f;
        Vector2 a = new Vector2(LP.x + 40f, LP.y - 70f), b = new Vector2(X(25) + 60f, LP.y - 70f);
        Vector2 f = Vector2.Lerp(a, b, e);
        bool down = t >= 0.15f && t < 1.5f;
        finger.rectTransform.anchoredPosition = new Vector2(f.x, -f.y);
        finger.rectTransform.localScale = Vector3.one * (down ? 0.8f : 1f);
        finger.color = new Color(Cobalt.r, Cobalt.g, Cobalt.b, t < 1.5f ? 0.85f : Mathf.Clamp01(1f - (t - 1.5f) / 0.4f) * 0.85f);
        float rp = Mathf.Clamp01((t - 1.5f) / 0.6f);
        fingerRing.rectTransform.anchoredPosition = finger.rectTransform.anchoredPosition;
        fingerRing.rectTransform.localScale = Vector3.one * (t < 1.5f ? 1f : 1f + rp * 1.6f);
        fingerRing.color = new Color(Cobalt.r, Cobalt.g, Cobalt.b, t < 1.5f ? (down ? 0.5f : 0.2f) : (1f - rp) * 0.6f);
        float dd = 25f * e;
        for (int i = 0; i < guideArc.Count; i++)
        {
            bool vis = drag > 0f && t < 1.9f;
            guideArc[i].enabled = vis && dd > 1f;
            if (!guideArc[i].enabled) continue;
            Vector2 q = Bez(dd, 0, i / (float)(guideArc.Count - 1));
            guideArc[i].rectTransform.anchoredPosition = new Vector2(q.x, -q.y);
            guideArc[i].color = new Color(Cobalt.r, Cobalt.g, Cobalt.b, t < 1.5f ? 0.5f : 0.5f * (1f - Mathf.Clamp01((t - 1.5f) / 0.4f)));
        }
    }

    // ───────── 几何 ─────────
    static float X(float v) => RX0 + v / 100f * (RX1 - RX0);
    static Vector2 Bez(float dd, int k, float t)
    {
        float x1 = X(dd), h = 150f + dd * 3.2f + k * 16f;
        Vector2 a = LP, c = new Vector2((LP.x + x1) / 2f, LP.y - h), b = new Vector2(x1, G - 10f);
        return (1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b;
    }
    static float Tan(float dd, int k, float t)       // 弧线在 t 处的角度（弧度，屏幕坐标：y 向下）
    {
        float x1 = X(dd), h = 150f + dd * 3.2f + k * 16f;
        Vector2 a = LP, c = new Vector2((LP.x + x1) / 2f, LP.y - h), b = new Vector2(x1, G - 10f);
        Vector2 v = 2 * (1 - t) * (c - a) + 2 * t * (b - c);
        return Mathf.Atan2(v.y, v.x);
    }
    void PlaceGlider(Vector2 p, float rot)
    {
        glider.anchoredPosition = new Vector2(p.x, -p.y);
        glider.localEulerAngles = new Vector3(0, 0, -rot * Mathf.Rad2Deg);
    }
    Vector2 Mouse()
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(stage, Input.mousePosition, null, out Vector2 lp);
        return new Vector2(lp.x + 960f, 540f - lp.y);
    }
    bool InSky(out Vector2 p) { p = Mouse(); return p.x >= 740 && p.x <= 1700 && p.y >= 130 && p.y <= 970; }

    // ───────── 零件 ─────────
    RectTransform Rect(string n, RectTransform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(n, typeof(RectTransform)); var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.anchoredPosition = pos; rt.sizeDelta = size; return rt;
    }
    Image Img(string n, float x, float y, float w, float h, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image)); var rt = go.GetComponent<RectTransform>(); rt.SetParent(stage, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        var im = go.GetComponent<Image>(); im.color = c; im.raycastTarget = false; return im;
    }
    static void Child(RectTransform parent, Graphic g) => g.rectTransform.SetParent(parent, true);
    void Line(float x, float y, float w, float h, float a) => Img("rule", x, y, w, h, new Color(Cobalt.r, Cobalt.g, Cobalt.b, a));
    TextMeshProUGUI Txt(string s, TMP_FontAsset f, float size, float x, float y, float w, float h, FontStyles st = FontStyles.Normal, TextAlignmentOptions al = TextAlignmentOptions.TopLeft)
    {
        var go = new GameObject("T", typeof(RectTransform), typeof(TextMeshProUGUI)); var rt = go.GetComponent<RectTransform>(); rt.SetParent(stage, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        var t = go.GetComponent<TextMeshProUGUI>(); if (f != null) t.font = f;
        t.text = s; t.fontSize = size; t.color = Cobalt; t.fontStyle = st; t.alignment = al; t.enableWordWrapping = w > 300 && h > 40; t.raycastTarget = false;
        return t;
    }
    TextMeshProUGUI Cap(string s, float x, float y, float w, TextAlignmentOptions al = TextAlignmentOptions.TopLeft)
    {
        var t = Txt(s, uiFont, 12f, x, y, w, 16, FontStyles.Normal, al); t.characterSpacing = 9f; return t;
    }
    Button MakeButton(string label, float x, float y, float w, float h, bool filled, System.Action onClick)
    {
        var im = Img("Button", x, y, w, h, filled ? Cobalt : new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.06f)); im.raycastTarget = true;
        if (!filled) { var o = im.gameObject.AddComponent<Outline>(); o.effectColor = Cobalt; o.effectDistance = new Vector2(1, -1); }
        var t = Txt(label, filled ? uiFont : textFont, filled ? 20f : 17f, 0, 0, w, h, filled ? FontStyles.Bold : FontStyles.Normal, TextAlignmentOptions.Center);
        t.rectTransform.SetParent(im.rectTransform, false); t.rectTransform.anchoredPosition = Vector2.zero; t.color = filled ? Paper : Cobalt;
        var b = im.gameObject.AddComponent<Button>(); b.onClick.AddListener(() => onClick()); b.targetGraphic = im;
        return b;
    }
    static Sprite[] Load3(string name)
    {
        var f = new Sprite[3]; for (int v = 0; v < 3; v++) f[v] = Resources.Load<Sprite>("Sprites/Fly/UI/" + name + v);
        return f[0] != null ? f : new Sprite[0];
    }
    void Play(string n) { var c = Resources.Load<AudioClip>("Audio/Fly/" + n); if (c != null) se.PlayOneShot(c); }
    static Sprite MakeDot()
    {
        int n = 32; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false); float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float dd = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)); tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(c - dd + 0.5f))); }
        tex.Apply(); return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}
