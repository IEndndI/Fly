using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// 抉择（分镜 #gchoice，2026-10-06 按分镜重做动画）：纸白面板 + 错位钴蓝底版，从上往下「擦」着展开；
// 每条选项 = 顶部细线（错峰伸出来）+ 字母 + 大字（28px）+ 悬停钴蓝下划线（0.2s 从左延伸）；
// 点选 = 整行钴蓝条从左扫过（420ms）、这条字反白、其余压暗 22% 并划掉；停 1.3 秒后整块淡出，最后才把 chosenIndex 交出去。
// 面板壳（ChoiceBox：Paper / Plate / 四条边 / 两个说明字）还是场景里的原件；选项行在代码里生成（不再用预制体）。
public class ChoiceMenu : MonoBehaviour
{
    public RectTransform container;    // 选项放这里（面板内、左右 24 的框）
    public Button buttonPrefab;        // 旧的按钮预制体（不再使用，槽位留着）
    public int chosenIndex = -1;       // 玩家选了第几个，-1 = 还没选（动画演完才写，DialogueManager 等它）
    public GameObject frame;           // 面板壳（ChoiceBox）

    static readonly Color Cobalt = new Color(0f, 0.278f, 0.671f);
    static readonly Color Paper = new Color(0.949f, 0.925f, 0.875f);

    class Opt
    {
        public RectTransform row, bar, ul, hl;
        public TextMeshProUGUI k, tx;
        public CanvasGroup g;
        public float ulK, ulTarget;                  // 悬停下划线：当前进度 / 目标
    }
    readonly List<Opt> opts = new List<Opt>();
    readonly List<string> texts = new List<string>();
    float revealT = -1f;                             // 展开时钟（时间戳，毫秒走）
    bool picking;

    void Awake()
    {
        if (frame != null) frame.SetActive(false);
    }

    public void Show(List<string> newTexts)
    {
        if (frame == null || container == null) { Debug.LogWarning("ChoiceMenu 的 frame / container 没接好"); return; }
        chosenIndex = -1; picking = false;
        ClearRows();
        frame.SetActive(true);
        var box = (RectTransform)frame.transform;
        if (frame.GetComponent<RectMask2D>() == null) frame.AddComponent<RectMask2D>();
        var cg = frame.GetComponent<CanvasGroup>(); if (cg == null) cg = frame.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        box.sizeDelta = new Vector2(box.sizeDelta.x, 0f);            // 从 0 高往下展开（分镜 clip 往下的近似）
        revealT = Time.realtimeSinceStartup;
        texts.Clear(); texts.AddRange(newTexts);
        for (int i = 0; i < newTexts.Count; i++) BuildRow(newTexts[i], i);
        PlaySfx("Fly/sfx_slide");
    }

    public void Clear()
    {
        ClearRows();
        if (frame != null)
        {
            var cg = frame.GetComponent<CanvasGroup>(); if (cg != null) cg.alpha = 1f;
            var box = (RectTransform)frame.transform;
            box.sizeDelta = new Vector2(box.sizeDelta.x, 400f);
            frame.SetActive(false);
        }
        revealT = -1f;
    }

    void ClearRows()
    {
        foreach (var o in opts) if (o.row != null) Destroy(o.row.gameObject);
        opts.Clear();
        if (container != null) foreach (Transform c in container) Destroy(c.gameObject);   // 保险：清掉遗留
    }

    void BuildRow(string s, int i)
    {
        // 直接挂在面板壳上，用分镜的面板坐标（.opt left/right 24、top 96+i*120）
        var row = Rect("Opt" + i, (RectTransform)frame.transform, new Vector2(24f, -(96f + i * 120f)), new Vector2(828f, 88f));
        var o = new Opt { row = row };
        o.g = row.gameObject.AddComponent<CanvasGroup>();
        var hit = row.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0);   // 接管点击

        // 钴蓝条：左右各多出 24（分镜 .bar 铺满整块面板），从左往右扫
        o.bar = Rect("Bar", row, new Vector2(-24f, 0f), new Vector2(876f, 88f));
        var bi = o.bar.gameObject.AddComponent<Image>(); bi.color = Cobalt; bi.raycastTarget = false;
        o.bar.localScale = new Vector3(0f, 1f, 1f);

        // 顶部细线（钴蓝 40%），展开时错峰伸出来
        o.hl = Rect("HL", row, new Vector2(0f, -0.5f), new Vector2(828f, 1f));
        var hi = o.hl.gameObject.AddComponent<Image>(); hi.color = new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.4f); hi.raycastTarget = false;
        o.hl.localScale = new Vector3(0f, 1f, 1f);

        o.k = Text(row, ((char)('A' + i)).ToString(), 14f, new Vector2(0f, -29f), new Vector2(56f, 30f), TextAlignmentOptions.MidlineLeft);
        o.tx = Text(row, s, 28f, new Vector2(56f, -24f), new Vector2(828f - 56f - 24f, 40f), TextAlignmentOptions.MidlineLeft);

        // 悬停下划线：2px，从 x=56 到右缘，底边往上 16
        o.ul = Rect("UL", row, new Vector2(56f, -70f), new Vector2(828f - 56f, 2f));
        var ui = o.ul.gameObject.AddComponent<Image>(); ui.color = Cobalt; ui.raycastTarget = false;
        o.ul.localScale = new Vector3(0f, 1f, 1f);

        var trig = row.gameObject.AddComponent<EventTrigger>();
        AddEntry(trig, EventTriggerType.PointerEnter, () => { if (!picking && chosenIndex < 0 && o.ulTarget < 1f) { o.ulTarget = 1f; PlaySfx("Fly/sfx_tick"); } });
        AddEntry(trig, EventTriggerType.PointerExit, () => { o.ulTarget = 0f; });
        AddEntry(trig, EventTriggerType.PointerClick, () => Pick(i));
        opts.Add(o);
    }

    void Pick(int i)
    {
        if (picking || chosenIndex >= 0 || i < 0 || i >= opts.Count) return;
        picking = true;
        PlaySfx("Fly/sfx_stamp");
        for (int j = 0; j < opts.Count; j++)
        {
            if (j == i)
            {
                opts[j].k.color = Paper; opts[j].tx.color = Paper;      // .pick：字反白
                opts[j].ul.gameObject.SetActive(false);
            }
            else
            {
                opts[j].g.alpha = 0.22f;                                 // .drop：压暗
                if (opts[j].tx != null) opts[j].tx.text = "<s>" + texts[j] + "</s>";   // .drop：划掉
            }
        }
        StartCoroutine(PickRun(i));
    }

    IEnumerator PickRun(int i)
    {
        var bar = opts[i].bar;
        for (float t = 0f; t < 0.42f; t += Time.deltaTime)                // 钴蓝条扫过（分镜 420ms eOut）
        {
            bar.localScale = new Vector3(Tweens.EaseOut(t / 0.42f), 1f, 1f);
            yield return null;
        }
        bar.localScale = Vector3.one;
        yield return new WaitForSeconds(1.3f);                            // 停 1.3 秒（分镜 wait 1300）
        var cg = frame.GetComponent<CanvasGroup>();
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)                 // 整块淡出（分镜 300ms）
        {
            cg.alpha = 1f - t / 0.3f;
            yield return null;
        }
        cg.alpha = 1f;
        chosenIndex = i;                                                  // 到这里才交棒，DialogueManager 开始往下走
    }

    void Update()
    {
        if (frame == null || !frame.activeSelf) return;
        if (revealT >= 0f)                                                // 展开：面板往下擦 + 细线错峰
        {
            var box = (RectTransform)frame.transform;
            float t = (Time.realtimeSinceStartup - revealT) * 1000f;
            box.sizeDelta = new Vector2(box.sizeDelta.x, 400f * Tweens.EaseOut(Mathf.Clamp01(t / 420f)));
            for (int i = 0; i < opts.Count; i++)
            {
                float u = Tweens.EaseOut(Seg(t, 160f + i * 90f, 460f + i * 90f));
                if (opts[i].hl != null) opts[i].hl.localScale = new Vector3(u, 1f, 1f);
            }
            if (t > 460f + opts.Count * 90f) revealT = -1f;
        }
        for (int i = 0; i < opts.Count; i++)                              // 悬停下划线：0.2s 伸 / 缩
        {
            var o = opts[i];
            o.ulK = Mathf.MoveTowards(o.ulK, o.ulTarget, Time.deltaTime / 0.2f);
            if (o.ul != null) o.ul.localScale = new Vector3(1f - Mathf.Pow(1f - o.ulK, 3f), 1f, 1f);
        }
    }

    static float Seg(float t, float a, float b) { return Mathf.Clamp01((t - a) / (b - a)); }

    // ───────── 小工具 ─────────
    RectTransform Rect(string n, RectTransform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(n, typeof(RectTransform));
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    TextMeshProUGUI Text(RectTransform parent, string s, float size, Vector2 pos, Vector2 box, TextAlignmentOptions al)
    {
        var go = new GameObject("T", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos; rt.sizeDelta = box;
        var t = go.GetComponent<TextMeshProUGUI>();
        var f = Font(); if (f != null) t.font = f;
        t.text = s; t.fontSize = size; t.color = Cobalt; t.alignment = al;
        t.enableWordWrapping = false; t.raycastTarget = false;
        return t;
    }

    static TMP_FontAsset font;
    static TMP_FontAsset Font()      // 场景里没给字体槽：从已加载资源里找 MainFont
    {
        if (font != null) return font;
        foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>()) if (f.name == "MainFont") return font = f;
        foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>()) return font = f;
        return null;
    }

    void AddEntry(EventTrigger trig, EventTriggerType type, System.Action a)
    {
        var e = new EventTrigger.Entry { eventID = type };
        e.callback.AddListener(_ => a());
        trig.triggers.Add(e);
    }

    void PlaySfx(string n)
    {
        var a = FindObjectOfType<AudioPlayer>();
        if (a != null) { a.PlaySfx(n); return; }
        var c = Resources.Load<AudioClip>("Audio/" + n);
        if (c != null) AudioSource.PlayClipAtPoint(c, Camera.main ? Camera.main.transform.position : Vector3.zero, SaveSystem.Sfx);
    }
}
