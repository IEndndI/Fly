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
        }
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
    public void SetNumeralReveal(float p) { numeralMask.sizeDelta = new Vector2(maskFullWidth * p, numeralMask.sizeDelta.y); }
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
        caption.alpha = place.alpha = title.alpha = english.alpha = quote.alpha = page.alpha = 0f;
        for (int i = 0; i < tocRows.Length; i++)
        {
            tocRows[i].alpha = 0f;
            tocRows[i].rectTransform.anchoredPosition = tocHome[i] + new Vector2(-24f, 0f);
        }
        // 扉页自己跑一个时钟（和分镜一样）：入场 1.25 秒；点一下 → 入场加速到 4×、省掉停留
        float ms = 0f;
        while (ms < CARD_HOLD)
        {
            ms += Time.deltaTime * (cardFast ? 4f : 1f);
            SetNumeralReveal(1f - Mathf.Pow(2f, -10f * Mathf.Clamp01(ms / 0.56f)));   // 章号：指数擦开（0.56 秒）
            SetTrailReveal(Mathf.Clamp01((ms - 0.3f) / 1f));                          // 蜡笔线 0.3 秒后开始画
            // 文字：五个位置错峰淡入（220/280/340/400/460 起，各 260 毫秒；分镜 renderGame）
            caption.alpha = Seg(ms, 220f, 480f);
            place.alpha   = Seg(ms, 280f, 540f);
            title.alpha = english.alpha = Seg(ms, 340f, 600f);
            quote.alpha   = Seg(ms, 400f, 660f);
            page.alpha    = Seg(ms, 460f, 720f);
            // 目录：一条一条从左边滑进来（300+i*55 起，各 340 毫秒，从 -24 滑到位）
            for (int i = 0; i < tocRows.Length; i++)
            {
                if (!tocRows[i].gameObject.activeSelf) continue;
                float u = Seg(ms, 300f + i * 55f, 640f + i * 55f), e = 1f - Mathf.Pow(1f - u, 3f);
                tocRows[i].alpha = u;
                tocRows[i].rectTransform.anchoredPosition = tocHome[i] + new Vector2(-24f * (1f - e), 0f);
                if (i < tocBacks.Length && tocBacks[i] != null)
                {
                    Color bc = tocBacks[i].color;
                    tocBacks[i].color = new Color(bc.r, bc.g, bc.b, tocBackA[i] * u);
                }
            }
            if (Input.GetMouseButtonDown(0)) cardFast = true;                         // 点击不算跳过，是加速
            if (cardFast && ms >= CARD_IN) break;                                     // 点过：入场走完就收
            yield return null;
        }
        // 收尾：全部归位（下一张扉页从干净状态开始）
        SetNumeralReveal(1f); SetTrailReveal(1f);
        caption.alpha = place.alpha = title.alpha = english.alpha = quote.alpha = page.alpha = 1f;
        for (int i = 0; i < tocRows.Length; i++)
        {
            tocRows[i].alpha = 1f;
            tocRows[i].rectTransform.anchoredPosition = tocHome[i];
            if (i < tocBacks.Length && tocBacks[i] != null)
            {
                Color bc = tocBacks[i].color;
                tocBacks[i].color = new Color(bc.r, bc.g, bc.b, tocBackA[i]);
            }
        }
    }

    // 分镜里的 seg(ms, a, b)：这一段的淡入进度（各元素错峰用）
    static float Seg(float ms, float a, float b) { return Mathf.Clamp01((ms - a) / (b - a)); }

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
