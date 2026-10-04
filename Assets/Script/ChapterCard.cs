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
    }
    public Chapter[] chapters;               // 文字都在 Inspector 里改

    [Header("零件（已经拖好）")]
    public RectTransform[] columns;          // T3 的 12 栏，轴心在顶边，scaleY 0 = 收起，1 = 盖满
    public CanvasGroup card;                 // 扉页内容（不含 12 栏）
    public RectTransform numeralMask;        // 章号的裁切框：揭开时从 0 拉到满宽
    public TextMeshProUGUI numeral, title, english, place, quote, page, caption;
    public Image[] tocBacks;                 // 目录每行的底（当前章反白）
    public TextMeshProUGUI[] tocRows;        // 目录每行的字

    [Header("时间")]
    public float columnTime = 0.45f;         // 每一栏落下 / 抽离用时
    public float columnStagger = 0.03f;      // 相邻两栏错开多久
    public float holdTime = 2.2f;            // 扉页停留

    static readonly Color Cobalt = new Color(0f, 0.278f, 0.671f);
    static readonly Color Paper = new Color(0.949f, 0.925f, 0.875f);
    float maskFullWidth;

    void Awake()
    {
        maskFullWidth = numeralMask.sizeDelta.x;
        SetColumns(0f);
        card.alpha = 0f;
        card.gameObject.SetActive(false);
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

    // ───────── 下面两个由你来写 ─────────

    // card:ch3 —— 12 栏落下盖住画面 → 扉页出现 → 停留
    public IEnumerator Cover(string id)
    {
        if (!SetContent(id)) yield break;

        // ① 12 栏落下：秒数从 0 走到 ColumnsTotal
        yield return Tweens.Tween(ColumnsTotal, t => t,
            p => SetColumnsAt(p * ColumnsTotal, true));                        // 空1：p 是 0~1，要换算成秒

        // ② 扉页出现：先显示出来，再淡入；章号从左往右揭开
        card.gameObject.SetActive(true);                               // 空2
        StartCoroutine(Tweens.Tween(0.6f, Tweens.Bezier(0f, 0f, 0f, 1f), p => SetNumeralReveal(p)));
        yield return Tweens.Tween(0.4f, Tweens.EaseOut, p => card.alpha = p);   // 空3

        // ③ 停留
        yield return new WaitForSeconds(holdTime);
        
    }

    // uncard —— 扉页收起 → 12 栏抽离，露出新场景
    public IEnumerator Reveal()
    {
        yield return Tweens.Tween(0.25f, t => t, p => card.alpha = 1f - p);
        card.gameObject.SetActive(false);
        yield return Tweens.Tween(ColumnsTotal, t => t,
            p => SetColumnsAt(p * ColumnsTotal, false));
    }
}
