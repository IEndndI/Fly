using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 章扉页：钴蓝满版、被裁切的大章号、目录、章名；进出场用 T3（12 栏依次落下 / 抽离）。
// 表格用法：effect 列写 card:ch3 → 12 栏落下盖住画面、扉页出现、停留；
//           下一行写新背景 + uncard → 扉页收起、12 栏抽离、露出新场景。
public class ChapterCard : MonoBehaviour
{
    [System.Serializable]
    public class Chapter
    {
        public string id = "ch1";            // 表格里 card: 后面写的名字
        public string numeral = "一";        // 大字章号
        public string title = "仲夏夜";
        public string english = "Midsummer Night";
        public string place = "七月 · 仲夏夜";
        [TextArea] public string quote = "";
        public string page = "003";
        public string ordinal = "first";     // 蜡笔写出来的英文序数词：Sprites/Fly/Card/trail_first_line0~2
    }
    public Chapter[] chapters;               // 文字都在 Inspector 里改

    [Header("零件（已经拖好）")]
    public RectTransform[] columns;          // T3 的 12 栏，轴心在顶边，scaleY 0 = 收起，1 = 盖满
    public CanvasGroup card;                 // 扉页内容（不含 12 栏）
    public RectTransform numeralMask;        // 章号的裁切框：揭开时从 0 拉到满宽
    public TextMeshProUGUI numeral, title, english, place, quote, page, caption;
    public Image[] tocBacks;                 // 目录每行的底（当前章反白）
    public TextMeshProUGUI[] tocRows;        // 目录每行的字
    public RectTransform trailMask;          // 蜡笔线的裁切框：从左往右拉开，线就像被画出来
    public BoilSprite trail;                 // 蜡笔线本身（3 帧抖动）

    [Header("时间")]
    public float columnTime = 0.45f;         // 每一栏落下 / 抽离用时
    public float columnStagger = 0.03f;      // 相邻两栏错开多久

    public const float CARD_IN = 1.25f;      // 扉页入场至少走这么久（分镜 CARD_IN）
    public const float CARD_HOLD = 2.6f;     // 扉页自己的时钟总长（分镜 CARD_HOLD）

    // 从标题的钴蓝色页（T4）进来时置上：游戏场景从「12 栏已盖住」开始，扉页无缝接上，不用再落栏（2026-10-06）
    public static bool CoveredEntry;
    bool entryCover;                         // 本次进场就是「盖着」的
    bool cardStarted;                        // 扉页已经接管（EntryLift 看到就退出，别把栏抽走）
    bool cardFast;                           // 点过一下：入场 4× 加速、省掉停留
    public bool EntryCoverActive { get { return entryCover; } }

    static readonly Color Cobalt = new Color(0f, 0.278f, 0.671f);
    static readonly Color Paper = new Color(0.949f, 0.925f, 0.875f);
    float maskFullWidth, trailFullWidth;
    Vector2[] tocHome;                       // 目录每一行的原位（滑入动画用）
    float[] tocBackA;                        // 目录底色原本的 alpha
    // 底色基准：在设计源头（Awake / SetContent）记下来，动画只乘透明度——卡片被打断也不会把半截颜色当成新基准
    Color capBase, plcBase, titBase, engBase, quoBase, pgBase;
    Color[] tocBase, dotBase, lineBase;
    TextMeshProUGUI numText;                 // 左上角的「N°02 · 第二章」（分镜 gcN，场景里原来没有，代码补的）
    Color numBase;

    void Awake()
    {
        maskFullWidth = numeralMask.sizeDelta.x;
        trailFullWidth = trailMask.sizeDelta.x;
        tocHome = new Vector2[tocRows.Length]; tocBackA = new float[tocRows.Length];
        for (int i = 0; i < tocRows.Length; i++)
        {
            tocHome[i] = tocRows[i].rectTransform.anchoredPosition;
            tocBackA[i] = tocBacks[i] != null ? tocBacks[i].color.a : 1f;
        }
        tocBase = new Color[tocRows.Length]; dotBase = new Color[tocRows.Length]; lineBase = new Color[tocRows.Length];
        var cc = caption.color; cc.a = 1f; caption.color = cc;              // 场景里它是 0（隐身），按分镜强制显示
        capBase = caption.color; plcBase = place.color; titBase = title.color;
        engBase = english.color; quoBase = quote.color; pgBase = page.color;
        BuildDetails();
        if (CoveredEntry)                    // 标题的钴蓝色页进来的：先当作已经盖住
        {
            CoveredEntry = false; entryCover = true;
            SetColumns(1f);
            StartCoroutine(EntryLift());
        }
        else SetColumns(0f);
        card.alpha = 0f;
        card.gameObject.SetActive(false);
    }

    // 没有扉页的进场（「继续」直接回到故事中间）：稍等，把 12 栏依次抽离，露出恢复好的场景；
    // 如果扉页在同一帧抢先把栏接管（「开始」流程），这里就什么都不做
    IEnumerator EntryLift()
    {
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            if (cardStarted) yield break;
            yield return null;
        }
        entryCover = false;
        yield return Tweens.Tween(ColumnsTotal, t => t, p => SetColumnsAt(p * ColumnsTotal, false));
    }

    // 把某一章的文字填进去；找不到这个 id 就返回 false
    public bool SetContent(string id)
    {
        int n = System.Array.FindIndex(chapters, c => c.id == id);
        if (n < 0) { Debug.LogWarning("章扉页里没有这一章：" + id); return false; }
        Chapter c = chapters[n];
        numeral.text = c.numeral; title.text = c.title; english.text = c.english;
        place.text = c.place; quote.text = c.quote; page.text = c.page;
        caption.text = "第" + c.numeral + "章 · Chapter " + (n + 1).ToString("00");
        Sprite[] frames = new Sprite[3];                                    // 这一章的蜡笔线，先收起来等着画
        for (int i = 0; i < 3; i++) frames[i] = Resources.Load<Sprite>("Sprites/Fly/Card/trail_" + c.ordinal + "_line" + i);
        trail.SetFrames(frames[0] != null ? frames : new Sprite[0]);
        SetTrailReveal(0f);
        for (int i = 0; i < tocRows.Length; i++)
        {
            bool has = i < chapters.Length, current = i == n;
            tocRows[i].gameObject.SetActive(has);
            tocBacks[i].enabled = current;                                   // 本章反白
            if (!has) continue;
            tocRows[i].text = (i + 1).ToString("00") + "　" + chapters[i].title + "<pos=88%>" + chapters[i].page;
            tocRows[i].color = current ? Cobalt : new Color(Paper.r, Paper.g, Paper.b, i > n ? 0.42f : 1f);  // 没读到的压暗
            if (tocLines != null && tocLines[i] != null) tocLines[i].enabled = !current;                      // 白底那行不要行线（分镜 .toc.on 无阴影）
            if (tocDots != null && tocDots[i] != null) tocDots[i].color = current ? Cobalt : new Color(Paper.r, Paper.g, Paper.b, 0.55f);   // .toc.on .ld 变钴蓝
            tocBase[i] = tocRows[i].color;                                                                   // 记基准（动画用，收尾还原用）
            if (tocDots != null && tocDots[i] != null) dotBase[i] = tocDots[i].color;
            if (tocLines != null && tocLines[i] != null) lineBase[i] = tocLines[i].color;
        }
        if (numText != null) numText.text = "N°" + (n + 1).ToString("00") + " · 第" + c.numeral + "章";       // 分镜 gcN
        return true;
    }

    // 12 栏一起摆到某个进度（每栏按自己的延迟错开）：p 是整体经过的秒数，cover = true 落下，false 抽离
    public void SetColumnsAt(float seconds, bool cover)
    {
        for (int i = 0; i < columns.Length; i++)
        {
            float t = Mathf.Clamp01((seconds - i * columnStagger) / columnTime);
            float e = t * t * (3f - 2f * t);                                  // 平滑的缓入缓出
            columns[i].localScale = new Vector3(1f, cover ? e : 1f - e, 1f);
        }
    }
    void SetColumns(float scaleY) { foreach (var c in columns) c.localScale = new Vector3(1f, scaleY, 1f); }
    public float ColumnsTotal { get { return columnTime + columnStagger * (columns.Length - 1); } }
    public void SetNumeralReveal(float p)
    {
        numeralMask.sizeDelta = new Vector2(maskFullWidth * p, numeralMask.sizeDelta.y);
        if (ticksMask != null) ticksMask.sizeDelta = new Vector2(ticksFullWidth * p, ticksMask.sizeDelta.y);   // 刻度跟着章号一起揭
    }
    public void SetTrailReveal(float p) { trailMask.sizeDelta = new Vector2(trailFullWidth * p, trailMask.sizeDelta.y); }

    // ───────── 下面两个由你来写 ─────────

    // card:ch3 —— 12 栏落下盖住画面 → 扉页出现 → 停留
    bool revealing;   // 收起已经开始：还没放完的落下就别再把扉页亮出来（防止点太快时扉页盖住不走）

    public IEnumerator Cover(string id)
    {
        revealing = false; cardStarted = true; cardFast = false;
        if (!SetContent(id)) yield break;

        // ① 12 栏落下：秒数从 0 走到 ColumnsTotal
        //（从标题的钴蓝色页进来的那次不用落：画面本来就是钴蓝满版，扉页直接接着出现）
        if (!entryCover)
            yield return Tweens.Tween(ColumnsTotal, t => t,
                p => SetColumnsAt(p * ColumnsTotal, true));                    // 空1：p 是 0~1，要换算成秒
        entryCover = false;

        if (revealing) yield break;
        // ② 扉页出现：每个部分用自己的入场（分镜同款）——章号从左往右擦开、文字错峰淡入、目录一条条滑进来
        card.gameObject.SetActive(true);                               // 空2
        card.alpha = 1f;                                               // 空3：整组不再统一淡入，下面逐个来
        // 底色基准在 Awake / SetContent 里已经记好（capBase / tocBase / dotBase / lineBase），
        // 这里只乘透明度——被打断也不会把半截颜色当成新基准
        // 扉页自己跑一个时钟（和分镜一样，单位毫秒）：入场 1.25 秒；点一下 → 入场加速到 4×、省掉停留
        float ms = 0f;
        while (ms < CARD_HOLD * 1000f)
        {
            ms += Time.deltaTime * 1000f * (cardFast ? 4f : 1f);
            SetNumeralReveal(1f - Mathf.Pow(2f, -10f * Mathf.Clamp01(ms / 560f)));    // 章号：指数擦开（560ms，分镜）
            SetTrailReveal(Mathf.Clamp01((ms - 300f) / 1000f));                       // 蜡笔线 300ms 后开始画
            // 文字：五个位置错峰淡入（220/280/340/400/460 起，各 260ms；分镜 renderGame）
            float titleU = Seg(ms, 340f, 600f);
            if (numText != null) numText.color = Fade(numBase, Seg(ms, 220f, 480f));   // 「N°0X · 第X章」第一档（分镜 gcN）
            place.color   = Fade(plcBase, Seg(ms, 280f, 540f));
            caption.color = Fade(capBase, titleU);                                     // 「第X章 · Chapter 0X」跟标题一组（分镜 gcT 容器）
            title.color   = Fade(titBase, titleU);
            english.color = Fade(engBase, titleU);
            quote.color   = Fade(quoBase, Seg(ms, 400f, 660f));
            page.color    = Fade(pgBase, Seg(ms, 460f, 720f));
            // 目录：一条一条从左边滑进来（300+i*55 起，各 340ms，从 -24 滑到位）
            for (int i = 0; i < tocRows.Length; i++)
            {
                if (!tocRows[i].gameObject.activeSelf) continue;
                float u = Seg(ms, 300f + i * 55f, 640f + i * 55f), e = 1f - Mathf.Pow(1f - u, 3f);
                tocRows[i].color = Fade(tocBase[i], u);
                tocRows[i].rectTransform.anchoredPosition = tocHome[i] + new Vector2(-24f * (1f - e), 0f);
                if (i < tocBacks.Length && tocBacks[i] != null)
                {
                    Color bc = tocBacks[i].color;
                    tocBacks[i].color = new Color(bc.r, bc.g, bc.b, tocBackA[i] * u);
                }
                if (tocDots != null && tocDots[i] != null) tocDots[i].color = Fade(dotBase[i], u);
                if (tocLines != null && tocLines[i] != null) tocLines[i].color = Fade(lineBase[i], u);
            }
            if (Input.GetMouseButtonDown(0)) cardFast = true;                         // 点击不算跳过，是加速
            if (cardFast && ms >= CARD_IN * 1000f) break;                             // 点过：入场走完就收
            yield return null;
        }
        // 收尾：全部还原到基准色（下一张扉页从干净状态开始）
        SetNumeralReveal(1f); SetTrailReveal(1f);
        caption.color = capBase; place.color = plcBase; title.color = titBase; english.color = engBase; quote.color = quoBase; page.color = pgBase;
        if (numText != null) numText.color = numBase;
        for (int i = 0; i < tocRows.Length; i++)
        {
            tocRows[i].color = tocBase[i];
            tocRows[i].rectTransform.anchoredPosition = tocHome[i];
            if (i < tocBacks.Length && tocBacks[i] != null)
            {
                Color bc = tocBacks[i].color;
                tocBacks[i].color = new Color(bc.r, bc.g, bc.b, tocBackA[i]);
            }
            if (tocDots != null && tocDots[i] != null) tocDots[i].color = dotBase[i];
            if (tocLines != null && tocLines[i] != null) tocLines[i].color = lineBase[i];
        }
    }

    // 底色不变，只乘一层透明度（分镜的 op(el, u)）
    static Color Fade(Color c, float a) { return new Color(c.r, c.g, c.b, c.a * a); }

    // 分镜里的 seg(ms, a, b)：这一段的淡入进度（各元素错峰用）
    static float Seg(float ms, float a, float b) { return Mathf.Clamp01((ms - a) / (b - a)); }

    // ───────── 分镜卡片上有、但场景里没做的细节（2026-10-06 对着分镜补齐）─────────
    // ① 切口下的刻度（drawCard 的 measured cut：第 5–12 栏每条栏边一齿，和章号同步揭开）
    // ② 目录每行的底部细线、标题与页码之间的虚线引导（.ld）
    // ③ 目录表头右边的「p.」
    RectTransform ticksMask;
    readonly float ticksFullWidth = 1232f;
    Image[] tocDots, tocLines;

    void BuildDetails()
    {
        // ⓪ 左上的「N°01 · 第一章」（分镜 gcN，左 120 上 80）：场景里缺这个元素，代码补
        {
            var go = new GameObject("NumLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform; rt.SetParent(card.transform, false);   // 挂进 Card：card 一收它跟着收
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(120f, -80f); rt.sizeDelta = new Vector2(828f, 20f);
            numText = go.GetComponent<TextMeshProUGUI>();
            numText.font = caption.font; numText.fontSize = 12f; numText.characterSpacing = 9f;
            numText.color = Paper; numText.alignment = TextAlignmentOptions.TopLeft;
            numText.enableWordWrapping = false; numText.raycastTarget = false;
            numBase = numText.color;
        }

        // ① 刻度：容器在 CutLine 下面一点（688,-672），宽 1232，用遮罩和章号一起从左往右揭
        ticksMask = Rect("CutTicks", (RectTransform)card.transform, new Vector2(688f, -672f), new Vector2(1232f, 20f));
        ticksMask.gameObject.AddComponent<RectMask2D>();
        for (int i = 5; i <= 12; i++)
        {
            float col = 120f + (i - 1) * 142f;
            Tick(ticksMask, col - 688f, 10f);              // 栏左边：长齿
            Tick(ticksMask, col + 118f - 688f, 6f);        // 栏右边：短齿
        }

        // ② 目录：虚线 + 行线（挂进 Toc1~5，跟着行一起动）
        tocDots = new Image[tocRows.Length]; tocLines = new Image[tocRows.Length];
        for (int i = 0; i < tocRows.Length; i++)
        {
            var row = tocRows[i].rectTransform;
            var ln = Rect("RowLine", row, new Vector2(0f, -39f), new Vector2(402f, 1f));
            var li = ln.gameObject.AddComponent<Image>();
            li.color = new Color(Paper.r, Paper.g, Paper.b, 0.2f); li.raycastTarget = false;
            tocLines[i] = li;
            var dt = Rect("Dots", row, new Vector2(110f, -20f), new Vector2(218f, 1f));
            var di = dt.gameObject.AddComponent<Image>();
            di.sprite = DotSprite(); di.type = Image.Type.Tiled;
            di.color = new Color(Paper.r, Paper.g, Paper.b, 0.55f); di.raycastTarget = false;
            tocDots[i] = di;
        }

        // ③ 表头的「p.」（贴着 TocHead 的右缘）
        var head = transform.Find("TocHead");
        if (head != null && tocRows.Length > 0)
        {
            var go = new GameObject("HeadP", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform; rt.SetParent(head, false);
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(0f, 20f);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = tocRows[0].font; t.text = "p."; t.fontSize = 12f; t.characterSpacing = 9f;
            t.color = new Color(Paper.r, Paper.g, Paper.b, 0.8f);
            t.alignment = TextAlignmentOptions.TopRight; t.enableWordWrapping = false; t.raycastTarget = false;
        }
    }

    void Tick(RectTransform parent, float x, float h)
    {
        var rt = Rect("Tick", parent, new Vector2(x, 0f), new Vector2(1f, h));
        var img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(Paper.r, Paper.g, Paper.b, 0.55f); img.raycastTarget = false;
    }

    static RectTransform Rect(string n, RectTransform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(n, typeof(RectTransform));
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    static Sprite dotLineSprite;
    static Sprite DotSprite()      // 6×1：2 实 4 空，Tiled 平铺成虚线
    {
        if (dotLineSprite != null) return dotLineSprite;
        var tex = new Texture2D(6, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        for (int x = 0; x < 6; x++) tex.SetPixel(x, 0, x < 2 ? Color.white : new Color(1f, 1f, 1f, 0f));
        tex.Apply();
        return dotLineSprite = Sprite.Create(tex, new Rect(0f, 0f, 6f, 1f), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    // 宽屏适配：等画布尺寸定下来后，把最右一栏补到画布右缘（16:9 时差值是 0，什么都不动）
    IEnumerator Start()
    {
        yield return null;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) yield break;
        float cw = ((RectTransform)canvas.transform).rect.width;
        float delta = cw - 1920f;
        if (delta < 0.5f) yield break;
        RectTransform last = null; float maxRight = -1f;
        foreach (var c in columns)
        {
            float right = c.anchoredPosition.x + c.sizeDelta.x * (1f - c.pivot.x);
            if (right > maxRight) { maxRight = right; last = c; }
        }
        if (last != null) last.sizeDelta = new Vector2(last.sizeDelta.x + delta, last.sizeDelta.y);
    }

    // —— 给系统页用的三个入口（2026-10-06）——
    public void SetColumnsNow(float scaleY) { SetColumns(scaleY); }

    // 跳行 / 读档前把扉页和栏都收干净（DialogueManager.JumpTo 和系统页读档用）
    public void ResetImmediate()
    {
        StopAllCoroutines();
        revealing = false; cardStarted = false; entryCover = false;
        card.alpha = 0f; card.gameObject.SetActive(false);
        SetColumns(0f);
    }

    // 「合上书，回到标题」/ Esc：12 栏落下盖住，然后回标题场景（分镜 exitGame）
    public void ExitToTitle()
    {
        DialogueManager.autoMode = DialogueManager.skipMode = false;
        StartCoroutine(ExitRoutine());
    }
    IEnumerator ExitRoutine()
    {
        var ap = FindObjectOfType<AudioPlayer>();
        if (ap != null) ap.PlaySfx("Fly/sfx_drop");
        yield return Tweens.Tween(ColumnsTotal, t => t, p => SetColumnsAt(p * ColumnsTotal, true));
        TitleScreen.CoveredEntry = true;      // 标题场景从「已盖住」开始，再用 12 栏抽离露出标题（分镜 exitGame）
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    // uncard —— 扉页收起 → 12 栏抽离，露出新场景
    public IEnumerator Reveal()
    {
        revealing = true;
        yield return Tweens.Tween(0.25f, t => t, p => card.alpha = 1f - p);
        card.gameObject.SetActive(false);
        yield return Tweens.Tween(ColumnsTotal, t => t,
            p => SetColumnsAt(p * ColumnsTotal, false));
    }
}
