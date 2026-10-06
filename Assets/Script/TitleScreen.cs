using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 标题界面（分镜第 3 幕）：整张画面是从分镜原样渲染出来的三帧（Resources/Sprites/Fly/UI/title_v0~2，按 12fps 轮流 = 蜡笔抖动），
// 左下钴蓝块里的菜单在这里用 UGUI 重做：悬停 / ↑↓ 选中时一条纸色从左边刷过去反白；Enter 或点击确认。
// 「开始」= 12 栏钴蓝落下盖住画面，再进游戏（TitleMenu.StartGame）；「合上笔记」= 退出（TitleMenu.QuitGame）。
// 继续 = 从自动存档接着读；回想 = 读过的句子；设置 = 文字速度、自动翻页、音量（SysPanels）。没有存档时继续 / 回想压暗。
// 界面零件在 Awake 里自己生成：挂到标题场景 Canvas 下一个空物体上，把 TitleMenu 拖进槽位即可（留空会自己找）。
public class TitleScreen : MonoBehaviour
{
    public TitleMenu titleMenu;
    public TMP_FontAsset labelFont;          // 菜单的中文（分镜：站酷快乐体）
    public TMP_FontAsset uiFont;             // 系统页的小字（MainFont）
    public TMP_FontAsset textFont;           // 回想里的正文（WenKai）
    public string bgm = "Audio/Fly/bgm_title";
    public float bgmVolume = 0.35f;

    static readonly Color Cobalt = new Color(0f, 0.278f, 0.671f);
    static readonly Color Paper = new Color(0.949f, 0.925f, 0.875f);

    class Item { public string num, zh, en; public bool enabled; public RectTransform inv; public float k; public CanvasGroup group; }
    readonly Item[] items = {
        new Item { num = "01", zh = "开始", en = "START", enabled = true },
        new Item { num = "02", zh = "继续", en = "CONTINUE" },
        new Item { num = "03", zh = "回想", en = "MEMORIES" },
        new Item { num = "04", zh = "设置", en = "SETTINGS" },
        new Item { num = "05", zh = "合上笔记", en = "CLOSE", enabled = true },
    };
    int sel = -1; bool leaving;
    RectTransform root; AudioSource music, se;
    RectTransform flood;                                          // 「开始」用的钴蓝色页（T4）
    RectTransform picture;                                        // 标题画面三帧
    float fitW = -1f;
    RectTransform[] coverCols;                                    // 从游戏回来时的 12 栏（先盖着，再抽离）
    public static bool CoveredEntry;                              // 游戏里「合上书 / Esc」置上：标题从「已盖住」开始

    void Awake()
    {
        if (titleMenu == null) titleMenu = FindObjectOfType<TitleMenu>();
        items[1].enabled = items[2].enabled = SaveSystem.HasSave; items[3].enabled = true;
        root = (RectTransform)transform;
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;

        // 画面：三帧轮流
        var bgGo = new GameObject("Picture", typeof(RectTransform), typeof(Image));
        var bgRt = bgGo.GetComponent<RectTransform>(); bgRt.SetParent(root, false);
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one; bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        picture = bgRt;
        bgGo.GetComponent<Image>().raycastTarget = false;
        var boil = bgGo.AddComponent<BoilSprite>();
        var frames = new Sprite[3];
        for (int i = 0; i < 3; i++) frames[i] = Resources.Load<Sprite>("Sprites/Fly/UI/title_v" + i);
        boil.SetFrames(frames[0] != null ? frames : new Sprite[0]);

        // 菜单：五行，每行 402 × 56，从 (120, 616) 起（AE 坐标）
        for (int i = 0; i < items.Length; i++) BuildItem(items[i], i);

        // 「开始」的钴蓝色页（T4）：先从选中那行的反白条位置开始，进游戏前再展开（2026-10-06，照分镜 startGame）
        var fGo = new GameObject("Flood", typeof(RectTransform), typeof(Image));
        flood = fGo.GetComponent<RectTransform>(); flood.SetParent(root, false);
        flood.anchorMin = flood.anchorMax = new Vector2(0f, 1f); flood.pivot = new Vector2(0f, 1f);
        flood.anchoredPosition = new Vector2(120f, -616f); flood.sizeDelta = new Vector2(402f, 56f);
        var fImg = fGo.GetComponent<Image>(); fImg.color = Cobalt; fImg.raycastTarget = false;
        fGo.SetActive(false);

        music = gameObject.AddComponent<AudioSource>(); music.loop = true; music.playOnAwake = false; music.volume = bgmVolume * SaveSystem.MusicScale;
        se = gameObject.AddComponent<AudioSource>(); se.playOnAwake = false; se.volume = SaveSystem.Sfx;
        var clip = Resources.Load<AudioClip>(bgm);
        if (clip != null) { music.clip = clip; music.Play(); }

        if (CoveredEntry)                     // 从游戏「合上书」回来：先盖着，再依次抽离露出标题（分镜 exitGame → 标题的 12 栏）
        {
            CoveredEntry = false;
            BuildCover();
            StartCoroutine(CoverLift());
        }
    }

    void BuildCover()
    {
        coverCols = new RectTransform[12];
        for (int i = 0; i < 12; i++)
        {
            float l = i == 0 ? 0 : 120 + i * 142 - 12, r = i == 11 ? 1920 : 120 + i * 142 + 118 + 12;
            var go = new GameObject("Cover" + (i + 1), typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(root, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(l, 0f); rt.sizeDelta = new Vector2(r - l + 1, 1080f);
            var img = go.GetComponent<Image>(); img.color = Cobalt; img.raycastTarget = false;
            coverCols[i] = rt;
        }
        float cw = root.rect.width;            // 宽屏：末栏补到画布右缘
        if (cw > 1920.5f) coverCols[11].sizeDelta = new Vector2(coverCols[11].sizeDelta.x + (cw - 1920f), 1080f);
    }

    IEnumerator CoverLift()
    {
        Play("Fly/sfx_drop");
        float total = 0.45f + 11 * 0.03f;
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            for (int i = 0; i < 12; i++)
            {
                float u = Mathf.Clamp01((t - i * 0.03f) / 0.45f);
                coverCols[i].localScale = new Vector3(1f, 1f - EaseInOut(u), 1f);   // 依次抽离（分镜 colWipe 的 cover=false）
            }
            yield return null;
        }
        foreach (var c in coverCols) c.gameObject.SetActive(false);
    }

    void BuildItem(Item it, int i)
    {
        var go = new GameObject("Item" + it.num, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(root, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(120f, -(616f + i * 56f)); rt.sizeDelta = new Vector2(402f, 56f);
        var hit = go.GetComponent<Image>(); hit.color = new Color(0, 0, 0, 0);       // 透明，只用来接鼠标
        it.group = go.GetComponent<CanvasGroup>(); it.group.alpha = it.enabled ? 1f : 0.42f;

        var rule = MakeRect("Rule", rt, new Vector2(0, 0), new Vector2(402, 1)); rule.GetComponent<Image>().color = new Color(Paper.r, Paper.g, Paper.b, 0.35f);
        BuildRow(rt, it, Paper);
        // 反白：纸色条 + 钴蓝字，宽度从 0 拉到满（用 RectMask2D 裁）
        var inv = MakeRect("Invert", rt, new Vector2(0, -1), new Vector2(0, 55));
        inv.gameObject.AddComponent<RectMask2D>();
        var fill = MakeRect("Fill", inv, Vector2.zero, new Vector2(402, 55)); fill.GetComponent<Image>().color = Paper;
        BuildRow(fill, it, Cobalt);
        it.inv = inv;

        if (!it.enabled) return;
        var trig = go.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        AddTrigger(trig, UnityEngine.EventSystems.EventTriggerType.PointerEnter, () => Select(i));
        AddTrigger(trig, UnityEngine.EventSystems.EventTriggerType.PointerClick, () => { Select(i); Confirm(); });
    }

    void BuildRow(RectTransform parent, Item it, Color c)
    {
        Text(parent, it.num, 12f, new Vector2(12, -28), new Vector2(40, 30), c, TextAlignmentOptions.MidlineLeft, null, 1f);
        Text(parent, it.zh, 24f, new Vector2(60, -28), new Vector2(260, 40), c, TextAlignmentOptions.MidlineLeft, labelFont, 0f);
        Text(parent, it.en, 11f, new Vector2(402 - 14 - 160, -28), new Vector2(160, 30), c, TextAlignmentOptions.MidlineRight, null, 8f);
    }

    RectTransform MakeRect(string n, RectTransform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        go.GetComponent<Image>().raycastTarget = false;
        return rt;
    }

    void Text(RectTransform parent, string s, float size, Vector2 pos, Vector2 box, Color c, TextAlignmentOptions al, TMP_FontAsset f, float spacing)
    {
        var go = new GameObject("T", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = box;
        var t = go.GetComponent<TextMeshProUGUI>(); if (f != null) t.font = f;
        t.text = s; t.fontSize = size; t.color = c; t.alignment = al; t.characterSpacing = spacing;
        t.enableWordWrapping = false; t.raycastTarget = false;
    }

    static void AddTrigger(UnityEngine.EventSystems.EventTrigger trig, UnityEngine.EventSystems.EventTriggerType type, System.Action a)
    {
        var e = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = type };
        e.callback.AddListener(_ => a());
        trig.triggers.Add(e);
    }

    void Select(int i)
    {
        if (leaving || SysPanels.UiOpen > 0 || i == sel || !items[i].enabled) return;
        sel = i; Play("Fly/sfx_tick");
    }

    // 宽屏适配（同 GameChrome）：画布 Expand，标题画面 cover 铺满、不拉伸
    void FitWide()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var crt = (RectTransform)canvas.transform;
        float cw = crt.rect.width, ch = crt.rect.height;
        if (cw <= 1f || Mathf.Abs(cw - fitW) < 0.5f) return;
        if (fitW < 0f)
        {
            var scaler = GetComponentInParent<CanvasScaler>();
            if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }
        fitW = cw;
        if (picture != null)
        {
            float cover = Mathf.Max(cw / 1920f, ch / 1080f);
            picture.anchorMin = picture.anchorMax = new Vector2(0.5f, 0.5f); picture.pivot = new Vector2(0.5f, 0.5f);
            picture.anchoredPosition = Vector2.zero; picture.sizeDelta = new Vector2(1920f, 1080f);
            picture.localScale = new Vector3(cover, cover, 1f);
        }
    }

    void Update()
    {
        FitWide();
        // 反白条：0.16 秒拉满 / 收回
        foreach (var it in items)
        {
            float target = items[sel < 0 ? 0 : sel] == it && sel >= 0 ? 1f : 0f;
            it.k = Mathf.MoveTowards(it.k, target, Time.deltaTime / 0.16f);
            float e = 1f - Mathf.Pow(1f - it.k, 3f);
            if (it.inv != null) it.inv.sizeDelta = new Vector2(402f * e, 55f);   // 重编译/销毁中途可能暂时为空
        }
        if (!leaving && music != null && se != null)   // 设置页里改音量：标题的音乐 / 音效实时跟着（进游戏的淡出期间不动它）
        {
            music.volume = bgmVolume * SaveSystem.MusicScale;
            se.volume = SaveSystem.Sfx;
        }
        if (leaving || SysPanels.UiOpen > 0) return;
        if (Input.GetKeyDown(KeyCode.DownArrow)) Step(1);
        if (Input.GetKeyDown(KeyCode.UpArrow)) Step(-1);
        if (sel >= 0 && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) Confirm();
    }

    void Step(int d)
    {
        int i = sel;
        for (int n = 0; n < items.Length; n++) { i = (i + d + items.Length) % items.Length; if (items[i].enabled) break; }
        Select(i);
    }

    void Confirm()
    {
        if (leaving || sel < 0 || SysPanels.UiOpen > 0) return;
        Play("Fly/sfx_select");
        if (items[sel].zh == "开始") { SaveSystem.PendingRow = -1; StartCoroutine(Begin()); }
        else if (items[sel].zh == "继续") OpenSys("load");      // 分镜：继续 = 打开读档页，点一个档位从那里继续读
        else if (items[sel].zh == "回想") OpenSys("log");
        else if (items[sel].zh == "设置") OpenSys("settings");
        else if (items[sel].zh == "合上笔记") titleMenu.QuitGame();
    }

    void OpenSys(string tab)
    {
        SysPanels.UiFont = uiFont; SysPanels.LabelFont = labelFont; SysPanels.TextFont = textFont;
        SysPanels.Show(tab);
    }

    // 「开始 / 继续」：选中那行的反白条变成钴蓝色页（T4），先横向拉满、再纵向涨满，然后进游戏；
    // 游戏场景的章扉页从同一片钴蓝里接着出现，中间没有缝（照分镜 startGame → enterGame，2026-10-06）
    IEnumerator Begin()
    {
        leaving = true; Play("Fly/sfx_whoosh");
        int i = Mathf.Max(0, sel);
        float x0 = 120f, y0 = 616f + i * 56f, w0 = 402f, h0 = 56f, total = 0.5f;
        float cw = root.rect.width, chh = root.rect.height;       // 铺满整块画布（宽屏上也不会漏边）
        flood.gameObject.SetActive(true); SetFlood(x0, y0, w0, h0);
        float v0 = music.volume;
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            if (t < 0.26f)                                        // ① 横拉满（260ms）
            {
                float e = EaseInOut(t / 0.26f), l = Mathf.Lerp(x0, 0f, e), r = Mathf.Lerp(x0 + w0, cw, e);
                SetFlood(l, y0, r - l, h0);
            }
            else                                                  // ② 竖涨满（240ms）
            {
                float e = EaseInOut((t - 0.26f) / 0.24f), top = Mathf.Lerp(y0, 0f, e), bot = Mathf.Lerp(y0 + h0, chh, e);
                SetFlood(0f, top, cw, bot - top);
            }
            music.volume = v0 * (1f - t / total);
            yield return null;
        }
        SetFlood(0f, 0f, cw, chh); music.volume = 0f;
        ChapterCard.CoveredEntry = true;                          // 游戏场景从「已盖住」开始，扉页不用再落栏
        yield return new WaitForSeconds(0.1f);
        titleMenu.StartGame();
    }

    void SetFlood(float x, float y, float w, float h)
    {
        flood.anchoredPosition = new Vector2(x, -y);
        flood.sizeDelta = new Vector2(w, h);
    }

    static float EaseInOut(float x) { return x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f; }

    void Play(string name)
    {
        var c = Resources.Load<AudioClip>("Audio/" + name);
        if (c != null) se.PlayOneShot(c);
    }
}
