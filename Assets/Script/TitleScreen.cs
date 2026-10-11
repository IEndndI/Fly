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
    Item[] items = {
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
    public static bool FromBlack;                                 // 结局播完置上：标题从黑场开始，0.9 秒淡出（分镜 finishEnding）
    bool entering;                                                // 开场视频之后的入场动画进行中：不接受操作

    void Awake()
    {
        if (titleMenu == null) titleMenu = FindObjectOfType<TitleMenu>();
        items[1].enabled = items[2].enabled = SaveSystem.HasSave; items[3].enabled = true;
        if (PlayerPrefs.GetInt("Fly.Cleared", 0) == 1)                      // 通关以后：「合上笔记」上面多一行「关于」
        {
            var l = new System.Collections.Generic.List<Item>(items);
            l.Insert(4, new Item { num = "05", zh = "关于", en = "ABOUT", enabled = true });
            l[5].num = "06"; items = l.ToArray();
        }
        root = (RectTransform)transform;
        PrintMargin.Create(root.parent, uiFont);                             // 宽屏 / 平板多出来的两边：印张的纸边（最底层）
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
        // 画里左下角印的是「V0.1」：盖一块同色的底，印上真实的版本号（Project Settings 里的 Version）
        var ver = MakeRect("Version", picture, new Vector2(116, -1028), new Vector2(52, 24));
        ver.GetComponent<Image>().color = new Color(0f, 0.267f, 0.639f);
        Text(ver, "V" + Application.version + " ·", 12f, new Vector2(4, -12), new Vector2(60, 24), new Color(0.93f, 0.91f, 0.86f), TextAlignmentOptions.MidlineLeft, uiFont, 0.5f);

        // 菜单：五行，每行 402 × 56，从 (120, 616) 起（AE 坐标）
        for (int i = 0; i < items.Length; i++) BuildItem(items[i], i);

        // 标题左下印着「N°00 · AI 说明」：点它再看一遍创作声明（照分镜 #tAI）
        var aiHit = MakeRect("AILink", root, new Vector2(330, -1030), new Vector2(120, 28));
        aiHit.GetComponent<Image>().color = new Color(0, 0, 0, 0); aiHit.GetComponent<Image>().raycastTarget = true;
        var aiTrig = aiHit.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        AddTrigger(aiTrig, UnityEngine.EventSystems.EventTriggerType.PointerClick, () =>
        {
            if (leaving || entering || AIDisclosure.Active || SysPanels.UiOpen > 0) return;
            Play("Fly/sfx_slide");
            AIDisclosure.ForceShow = true;
            var go = new GameObject("AIDisclosure", typeof(RectTransform)); go.transform.SetParent(root.parent, false);
            var d = go.AddComponent<AIDisclosure>(); d.uiFont = uiFont; d.textFont = textFont; d.handFont = labelFont;
        });

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

        int ai = System.Array.FindIndex(items, x => x.zh == "关于");
        if (ai >= 0 && PlayerPrefs.GetInt("Fly.AboutSeen", 0) == 0) StartCoroutine(AboutReveal(ai));   // 第一次通关回来：「关于」单独登场 + 指引

        if (FromBlack)                        // 从结局回来：黑场 0.9 秒淡出（分镜 finishEnding 的 quit 黑幕）
        {
            FromBlack = false;
            StartCoroutine(BlackFade());
        }
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
        var ex = UIFrame.Extra(root);                                     // 屏幕比 16:9 宽 / 高时，两边各多出多少
        for (int i = 0; i < 12; i++)
        {
            float l = i == 0 ? -ex.x : 120 + i * 142 - 12, r = i == 11 ? 1920 + ex.x : 120 + i * 142 + 118 + 12;
            var go = new GameObject("Cover" + (i + 1), typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(root, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(l, ex.y); rt.sizeDelta = new Vector2(r - l + 1, 1080f + 2f * ex.y);
            var img = go.GetComponent<Image>(); img.color = Cobalt; img.raycastTarget = false;
            coverCols[i] = rt;
        }
    }

    // 「关于」第一次出现：别的入场都走完以后，这一行从左边滑进来、纸色条刷过一遍、盖个章；
    // 右边挂一张小标签「← 新的一页」一直轻轻晃，直到第一次点开「关于」
    RectTransform aboutTag; CanvasGroup aboutTagGroup;
    IEnumerator AboutReveal(int ai)
    {
        var it = items[ai]; var rt = (RectTransform)it.group.transform;
        float y = rt.anchoredPosition.y, wait = 0f;
        it.group.blocksRaycasts = false;
        while (wait < 0.8f)                                                 // 等开场 / 黑场 / 声明 / 抽栏都结束，再多等 0.8 秒
        {
            it.group.alpha = 0f; it.group.blocksRaycasts = false;
            bool busy = entering || leaving || AIDisclosure.Active || SysPanels.UiOpen > 0;
            wait = busy ? 0f : wait + Time.deltaTime;
            yield return null;
        }
        // 纸色条：从左刷到右，再收回去
        var flash = MakeRect("AboutFlash", rt, new Vector2(0, -1), new Vector2(0, 55)); flash.GetComponent<Image>().color = Paper;
        Play("Fly/sfx_slide");
        for (float t = 0f; t < 1.1f; t += Time.deltaTime)
        {
            float u = Mathf.Clamp01(t / 0.45f), e = 1f - Mathf.Pow(1f - u, 3f);
            rt.anchoredPosition = new Vector2(120f - 60f * (1f - e), y);
            it.group.alpha = e;
            float w = t < 0.45f ? 402f * e : 402f * (1f - EaseInOut(Mathf.Clamp01((t - 0.55f) / 0.45f)));
            flash.sizeDelta = new Vector2(w, 55f);
            flash.anchoredPosition = new Vector2(t < 0.45f ? 0f : 402f - w, -1f);
            yield return null;
        }
        Destroy(flash.gameObject);
        rt.anchoredPosition = new Vector2(120f, y); it.group.alpha = 1f; it.group.blocksRaycasts = true;
        Play("Fly/sfx_stamp");

        // 指引标签
        aboutTag = MakeRect("AboutTag", root, new Vector2(120 + 402 + 24, y - 10), new Vector2(286, 36));
        aboutTag.GetComponent<Image>().color = Paper;
        var tl = MakeRect("Edge", aboutTag, Vector2.zero, new Vector2(4, 36)); tl.GetComponent<Image>().color = Cobalt;
        Text(aboutTag, "← NEW · 通关后多出来的一页", 15f, new Vector2(16, -18), new Vector2(262, 30), Cobalt, TextAlignmentOptions.MidlineLeft, uiFont, 1f);
        aboutTagGroup = aboutTag.gameObject.AddComponent<CanvasGroup>(); aboutTagGroup.blocksRaycasts = false;
        for (float t = 0f; ; t += Time.deltaTime)
        {
            bool seen = PlayerPrefs.GetInt("Fly.AboutSeen", 0) == 1 || leaving;
            if (seen)
            {
                aboutTagGroup.alpha -= Time.deltaTime / 0.3f;
                if (aboutTagGroup.alpha <= 0f) { Destroy(aboutTag.gameObject); yield break; }
            }
            else aboutTagGroup.alpha = Mathf.Clamp01(t / 0.3f);
            aboutTag.anchoredPosition = new Vector2(120 + 402 + 24 + Mathf.Sin(t * 4f) * 6f * Mathf.Clamp01(t / 0.3f) + 30f * (1f - Mathf.Clamp01(t / 0.3f)), y - 10);
            yield return null;
        }
    }

    IEnumerator CoverLift()
    {
        yield return null;                                                   // 等画布尺寸定下来（Awake 时还没按屏幕算好），再把两边的栏伸到屏幕边
        var ex = UIFrame.Extra(root);
        coverCols[0].anchoredPosition = new Vector2(-ex.x, ex.y);
        coverCols[0].sizeDelta = new Vector2(120 + 118 + 12 + ex.x + 1, 1080f + 2f * ex.y);
        float l11 = 120 + 11 * 142 - 12;
        coverCols[11].sizeDelta = new Vector2(1920f + ex.x - l11 + 1, 1080f + 2f * ex.y);
        for (int i = 1; i < 11; i++) { coverCols[i].anchoredPosition = new Vector2(coverCols[i].anchoredPosition.x, ex.y); coverCols[i].sizeDelta = new Vector2(coverCols[i].sizeDelta.x, 1080f + 2f * ex.y); }
        coverCols[11].anchoredPosition = new Vector2(l11, ex.y);
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
        if (leaving || entering || SysPanels.UiOpen > 0 || i == sel || !items[i].enabled) return;
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
            picture.anchorMin = picture.anchorMax = new Vector2(0.5f, 0.5f); picture.pivot = new Vector2(0.5f, 0.5f);
            picture.anchoredPosition = Vector2.zero; picture.sizeDelta = new Vector2(1920f, 1080f);
            picture.localScale = Vector3.one;                                     // v1.1：不再放大裁切，菜单和画对得上
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
        if (!leaving && !entering && music != null && se != null)   // 设置页里改音量：标题的音乐 / 音效实时跟着（进游戏的淡出期间不动它）
        {
            music.volume = bgmVolume * SaveSystem.MusicScale;
            se.volume = SaveSystem.Sfx;
        }
        if (leaving || entering || AIDisclosure.Active || SysPanels.UiOpen > 0) return;
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
        if (leaving || entering || sel < 0 || SysPanels.UiOpen > 0) return;
        Play("Fly/sfx_select");
        if (items[sel].zh == "开始") { SaveSystem.PendingRow = -1; StartCoroutine(Begin()); }
        else if (items[sel].zh == "继续") OpenSys("load");      // 分镜：继续 = 打开读档页，点一个档位从那里继续读
        else if (items[sel].zh == "回想") OpenSys("log");
        else if (items[sel].zh == "设置") OpenSys("settings");
        else if (items[sel].zh == "关于") { PlayerPrefs.SetInt("Fly.AboutSeen", 1); PlayerPrefs.Save(); AboutPanel.Open(root.parent, uiFont, textFont, labelFont); }
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
        var ex = UIFrame.Extra(root);                             // 铺满整块屏幕（宽屏 / 平板上也不会漏边）
        float L0 = -ex.x, cw = 1920f + ex.x, T0 = -ex.y, chh = 1080f + ex.y;
        flood.gameObject.SetActive(true); SetFlood(x0, y0, w0, h0);
        float v0 = music.volume;
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            if (t < 0.26f)                                        // ① 横拉满（260ms）
            {
                float e = EaseInOut(t / 0.26f), l = Mathf.Lerp(x0, L0, e), r = Mathf.Lerp(x0 + w0, cw, e);
                SetFlood(l, y0, r - l, h0);
            }
            else                                                  // ② 竖涨满（240ms）
            {
                float e = EaseInOut((t - 0.26f) / 0.24f), top = Mathf.Lerp(y0, T0, e), bot = Mathf.Lerp(y0 + h0, chh, e);
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

    // ───────── 开场视频之后：标题从视频最后一帧上铺出来（分镜 20.5–22.6 秒）─────────
    // OpeningPlayer 播放时先把标题藏起来
    // 开场视频一开始就把标题音乐淡进来（视频本身不出声）
    public void StartMusic(float fadeIn)
    {
        if (music.clip == null || music.isPlaying) return;
        music.volume = 0f; music.Play(); StartCoroutine(FadeMusic(bgmVolume * SaveSystem.MusicScale, fadeIn));
    }
    IEnumerator FadeMusic(float to, float time)
    {
        float from = music.volume;
        for (float t = 0f; t < time; t += Time.deltaTime) { music.volume = Mathf.Lerp(from, to, t / time); yield return null; }
        music.volume = to;
    }

    public void PrepareForOpening()
    {
        entering = true;
        picture.gameObject.SetActive(false);
        foreach (var it in items) { it.group.alpha = 0f; it.group.blocksRaycasts = false; }
        music.Stop();
    }

    // 12 栏从上往下依次铺出整张标题画面（每栏 0.45 秒、错开 0.06 秒），菜单一行行淡入，音乐淡入
    public IEnumerator Entrance(System.Action done)
    {
        FitWide();
        var boilSrc = picture.GetComponent<BoilSprite>();
        var corners = new Vector3[4]; picture.GetWorldCorners(corners);       // [1] = 左上角
        var strips = new RectTransform[12]; var sx = UIFrame.Extra(root);
        // 视频停在「飞」字下半截还在横条拼合的那一帧：右下角那块先用 0.3 秒淡成完整的字，不然栏铺到之前会露着几条白缝
        var patch = new GameObject("FeiPatch", typeof(RectTransform), typeof(RectMask2D), typeof(CanvasGroup)).GetComponent<RectTransform>();
        patch.SetParent(root, false); patch.SetSiblingIndex(picture.GetSiblingIndex() + 1);
        patch.anchorMin = new Vector2(0.7f, 0f); patch.anchorMax = new Vector2(1f, 0.56f); patch.offsetMin = patch.offsetMax = Vector2.zero;
        var patchGroup = patch.GetComponent<CanvasGroup>(); patchGroup.alpha = 0f; patchGroup.blocksRaycasts = false;
        {
            var pg = new GameObject("Pic", typeof(RectTransform), typeof(Image));
            var prt = pg.GetComponent<RectTransform>(); prt.SetParent(patch, false);
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 1f); prt.pivot = new Vector2(0f, 1f);
            prt.sizeDelta = new Vector2(1920f, 1080f); prt.localScale = picture.localScale; prt.position = corners[1];
            pg.GetComponent<Image>().raycastTarget = false;
            pg.AddComponent<BoilSprite>().SetFrames(boilSrc.frames);
        }
        for (int i = 0; i < 12; i++)
        {
            float l = i == 0 ? -sx.x : 120 + i * 142 - 12, r = i == 11 ? 1920f + sx.x : 120 + i * 142 + 118 + 12;
            var sg = new GameObject("Strip" + (i + 1), typeof(RectTransform), typeof(RectMask2D));
            var srt = sg.GetComponent<RectTransform>(); srt.SetParent(root, false); srt.SetSiblingIndex(picture.GetSiblingIndex() + 1);
            srt.anchorMin = srt.anchorMax = new Vector2(0f, 1f); srt.pivot = new Vector2(0f, 1f);
            srt.anchoredPosition = new Vector2(l, sx.y); srt.sizeDelta = new Vector2(r - l + 1, 0f);
            var ig = new GameObject("Pic", typeof(RectTransform), typeof(Image));
            var irt = ig.GetComponent<RectTransform>(); irt.SetParent(srt, false);
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 1f); irt.pivot = new Vector2(0f, 1f);
            irt.sizeDelta = new Vector2(1920f, 1080f); irt.localScale = picture.localScale; irt.position = corners[1];
            ig.GetComponent<Image>().raycastTarget = false;
            var b = ig.AddComponent<BoilSprite>(); b.SetFrames(boilSrc.frames);
            strips[i] = srt;
        }
        float ch = root.rect.height + 2f * sx.y;
        if (music.clip != null && !music.isPlaying) { music.volume = 0f; music.Play(); }
        float target = bgmVolume * SaveSystem.MusicScale, total = 0.45f + 11 * 0.06f, mv0 = music.volume;
        Play("Fly/sfx_slide"); int ticks = 0; bool stamped = false;
        for (float t = 0f; t < total + 1.2f; t += Time.deltaTime)
        {
            for (int i = 0; i < 12; i++)
            {
                float u = Mathf.Clamp01((t - i * 0.06f) / 0.45f), e = 1f - Mathf.Pow(1f - u, 3f);
                strips[i].sizeDelta = new Vector2(strips[i].sizeDelta.x, ch * e);
            }
            patchGroup.alpha = Mathf.Clamp01(t / 0.3f);
            for (int i = 0; i < items.Length; i++)                                 // 菜单：铺到一半以后一行行淡入（分镜 21.55 + i × 0.1）
            {
                float a = Mathf.Clamp01((t - 1.05f - i * 0.1f) / 0.3f);
                items[i].group.alpha = a * (items[i].enabled ? 1f : 0.42f);
            }
            music.volume = Mathf.Lerp(mv0, target, Mathf.Clamp01(t / 1.5f));
            while (ticks < 5 && t >= 0.95f + ticks * 0.1f) { Play("Fly/sfx_tick"); ticks++; }   // 菜单一行行落下（分镜 21.45–21.85）
            if (!stamped && t >= 1.5f) { stamped = true; Play("Fly/sfx_stamp"); }                // 印章（分镜 22.0）
            yield return null;
        }
        picture.gameObject.SetActive(true);
        foreach (var srt in strips) Destroy(srt.gameObject);
        Destroy(patch.gameObject);
        foreach (var it in items) { it.group.alpha = it.enabled ? 1f : 0.42f; it.group.blocksRaycasts = true; }
        entering = false;
        done?.Invoke();
    }

    IEnumerator BlackFade()
    {
        var go = new GameObject("BlackFade", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(root, false); rt.SetAsLastSibling();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>(); img.color = new Color(0.043f, 0.043f, 0.051f, 1f);
        entering = true;
        yield return new WaitForSeconds(0.15f);
        for (float t = 0f; t < 0.9f; t += Time.deltaTime) { img.color = new Color(0.043f, 0.043f, 0.051f, 1f - t / 0.9f); yield return null; }
        Destroy(go); entering = false;
    }

    static float EaseInOut(float x) { return x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f; }

    void Play(string name)
    {
        var c = Resources.Load<AudioClip>("Audio/" + name);
        if (c != null) se.PlayOneShot(c);
    }
}
