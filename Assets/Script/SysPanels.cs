using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

// 系统页（分镜第 7 幕）：存档 / 读档 / 回想 / 设置 四个整页（2026-10-06 按分镜完整重做）。
// 打开时：背后的画面先被「印」成钴蓝单色网点（15° 网角），钴蓝面板从右侧滑入第 5–12 栏（x 676 起），
// 里面嵌一张向右出血的纸白版面；左边一块纸色小页写页名和「现在 · 第x章 c.idx」。
// 键盘（分镜 sysKey）：1–4 切页、方向键选择、Enter 确认、Esc 返回。
// 标题的「继续」= 打开读档页；游戏里快捷栏 05 Menu = 打开设置页（分镜同款）。
public class SysPanels : MonoBehaviour
{
    public static int UiOpen;                                   // >0：有系统页开着（DialogueManager 不响应点击）

    // 字体登记处：调用方在 Show 之前把自己手里的字体写进来（谁有给谁，没登记就用回退）
    public static TMP_FontAsset UiFont, LabelFont, TextFont;

    static readonly Color Cobalt = new Color(0f, 0.278f, 0.671f);
    static readonly Color Paper = new Color(0.949f, 0.925f, 0.875f);
    public static readonly string[] ChapterTitles = { "", "仲夏夜", "最高的地方", "折角", "鸟群", "十一月" };
    static readonly string[] ChapterNums = { "", "一", "二", "三", "四", "五" };
    static readonly string[] TabIds = { "save", "load", "log", "settings" };
    static readonly string[][] TabInfo = {
        new[] { "存档", "Save · 12 个档位" },
        new[] { "读档", "Load · 12 个档位" },
        new[] { "回想", "Log · 读过的句子" },
        new[] { "设置", "Settings" },
    };

    // ───────── 派生数据 ─────────
    class RowInfo { public int chap; public int serial; public string bg; }
    class LogRow { public int row, chap, serial; public string name, text; public bool book, think; }
    class SlotData { public bool has; public int row; public string date; public bool auto; }

    string tab;
    bool inGame;
    DialogueManager dm;
    List<DialogueLine> lines = new List<DialogueLine>();
    RowInfo[] rowInfo = new RowInfo[0];
    SlotData[] slots = new SlotData[SaveSystem.SlotCount];

    // ───────── 零件 ─────────
    RectTransform root, panel, sheet;
    Image htone;
    RectTransform slabelRt;
    CanvasGroup slabelGroup;
    TextMeshProUGUI slT, slE, slM;
    readonly List<RectTransform> tabCells = new List<RectTransform>();
    readonly List<TextMeshProUGUI> tabZhs = new List<TextMeshProUGUI>();
    readonly List<TextMeshProUGUI> tabEns = new List<TextMeshProUGUI>();
    readonly List<TextMeshProUGUI> tabNums = new List<TextMeshProUGUI>();
    readonly List<Image> tabLines = new List<Image>();

    // 各页运行时状态
    int slotSel = -1;
    readonly List<RectTransform> slotCells = new List<RectTransform>();
    readonly List<RectTransform> slotRings = new List<RectTransform>();
    readonly List<RectTransform> slotThumbMasks = new List<RectTransform>();
    int logPage, logPages;
    readonly List<RectTransform> logPageRts = new List<RectTransform>();
    class SetRow
    {
        public string zh, en; public float min, max, step;
        public System.Func<float> get; public System.Action<float> set;
        public System.Func<float, string> show; public float[] opts;
        public RectTransform knob; public TextMeshProUGUI val, zhT;
        public System.Action refresh;                 // 就地刷新（拖动时不能重建整页）
    }
    readonly List<SetRow> setRows = new List<SetRow>();
    int setSel = -1;

    enum Phase { Opening, Open, Closing }
    Phase phase = Phase.Opening;
    float phaseStart = -1f;                                                  // 用时间戳起算：生成网点的卡顿不会让动画跳过去

    // ───────── 打开 / 关闭 ─────────
    public static void Show(string tab)
    {
        if (FindObjectOfType<SysPanels>() != null) return;                       // 一次只开一页
        var canvas = FindTopCanvas();
        if (canvas == null) return;
        var go = new GameObject("SysPanel", typeof(RectTransform));
        var rt = (RectTransform)go.transform; rt.SetParent(canvas.transform, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();
        var p = go.AddComponent<SysPanels>();
        p.StartCoroutine(p.OpenFlow(tab));
    }

    // 开页流程（对照分镜 composeScreen）：先把 UI 收起来、等这一帧渲染完 → 把「只有场景」的画面印成网点 → 放回 UI → 建系统页。
    // 印的东西 = 背景 + 立绘（对话框、正文、四角小字、快捷栏都不印）。
    IEnumerator OpenFlow(string wantTab)
    {
        inGame = FindObjectOfType<DialogueManager>() != null;
        dm = FindObjectOfType<DialogueManager>();
        var hidden = HideUiForPrint();
        yield return new WaitForEndOfFrame();                                    // 必须是「帧末」：CaptureScreenshotAsTexture 只能在这一刻拿到刚渲染的画面
        var tone = MakeHalftone();                                               // （原来用 yield return null，拿到的是上一帧——UI 还没藏起来的那张，印出来全是 UI）
        foreach (GameObject go in hidden) if (go != null) go.SetActive(true);    // UI 放回去
        Init(wantTab, tone);
    }

    List<GameObject> HideUiForPrint()
    {
        // 印的对象 = 当前画面的「背景 + 立绘」：画布上除底层的几样之外全部藏一帧——对话框、正文、四角小字、
        // 快捷栏、网格、书角、扉页……一个都不印。保留：Background（底色）+ BG_Line（背景的线稿层，房间是
        // 底色和线稿叠出来的）+ Stage（立绘）；标题场景再加上 TitleScreen 那张画面（菜单单独藏）。
        // 以前是逐个点名藏，老是漏（新加的装饰没人记得加进名单），干脆反着来：只留底和人物。
        var list = new List<GameObject>();
        var canvas = FindTopCanvas();
        if (canvas != null)
            foreach (Transform c in canvas.transform)
            {
                string n = c.name;
                if (n == "Background" || n == "BG_Line" || n == "Stage" || n == "TitleScreen" || n == "SysPanel") continue;   // SysPanel 是这页自己，别动
                Hide(list, c.gameObject);
            }
        for (int i = 1; i <= 5; i++) Hide(list, GameObject.Find("Canvas/TitleScreen/Item0" + i));   // 标题的菜单不印（画面留着）
        return list;
    }

    void Hide(List<GameObject> list, GameObject go)
    {
        if (go == null || !go.activeSelf) return;
        go.SetActive(false); list.Add(go);
    }

    void Init(string wantTab, Texture2D tone)
    {
        if (wantTab == "save" && !inGame) wantTab = "load";                      // 标题上不能存档（分镜 setTab）
        tab = wantTab;

        var loader = GetComponent<DialogueLoader>();
        if (loader == null) loader = gameObject.AddComponent<DialogueLoader>();
        lines = loader.LoadDialogueFromFile("Fly");
        ScanRows();
        ReadSlots();

        root = (RectTransform)transform;
        BuildUI(tone);
        SetTab(tab);

        UiOpen++;
        PlaySfx("Fly/sfx_print"); PlaySfx("Fly/sfx_slide");
        phase = Phase.Opening; phaseStart = -1f;
    }

    bool closing;
    public void Close()
    {
        if (closing) return;
        closing = true; phase = Phase.Closing; phaseStart = -1f;
        PlaySfx("Fly/sfx_slide");
        UiOpen = Mathf.Max(0, UiOpen - 1);
        DialogueManager.blockUntil = Time.time + 0.15f;                          // 关掉的那一下不要顺手翻页
    }
    void CloseNow()                                                              // 不等动画，直接销毁（读档跳转用）
    {
        if (closing) return;
        closing = true;
        UiOpen = Mathf.Max(0, UiOpen - 1);
        DialogueManager.blockUntil = Time.time + 0.15f;
        Destroy(gameObject);
    }

    void DiscardVisuals()                                                        // 立刻看不见、也不再拦点击（协程还要继续跑的事用）
    {
        if (closing) return;
        closing = true;
        UiOpen = Mathf.Max(0, UiOpen - 1);
        DialogueManager.blockUntil = Time.time + 0.15f;
        var cg = gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;
    }
    void OnDestroy() { if (!closing) UiOpen = Mathf.Max(0, UiOpen - 1); }

    // ───────── 表格扫描：每一行属于第几章、第几句、当前背景 ─────────
    void ScanRows()
    {
        int n = lines.Count;
        rowInfo = new RowInfo[n];
        string sec = ""; int chap = 1, serial = 0; string bg = "";
        for (int i = 0; i < n; i++)
        {
            var l = lines[i];
            if (!string.IsNullOrEmpty(l.flow) && l.flow.StartsWith("label:"))
            {
                sec = l.flow.Substring(6).Trim();
                chap = sec == "第一章" ? 1 : sec == "第二章" ? 2 : sec == "第三章" ? 3 : sec == "第四章" ? 4 : 5;
                serial = 0;
            }
            if (!string.IsNullOrEmpty(l.backgroundSpriteName)) bg = l.backgroundSpriteName;
            bool textRow = !string.IsNullOrEmpty(l.text) && (l.flow == null || !l.flow.StartsWith("option:"));
            if (textRow) serial++;
            rowInfo[i] = new RowInfo { chap = chap, serial = serial, bg = bg };
        }
    }

    void ReadSlots()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            int row = SaveSystem.SlotRow(i);
            slots[i] = new SlotData { has = row >= 0, row = row, date = SaveSystem.SlotDate(i), auto = SaveSystem.SlotAuto(i) };
        }
    }

    // ───────── 建界面 ─────────
    void BuildUI(Texture2D tone)
    {
        // 背后印刷：开页前采样的画面，按 15° 网角铺钴蓝圆点（分镜 renderHalftone）
        htone = NewImage("HTone", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        htone.raycastTarget = true;                                  // 盖住整屏、挡住后面的点击
        if (tone != null)
        {
            htone.sprite = Sprite.Create(tone, new Rect(0, 0, tone.width, tone.height), new Vector2(0.5f, 0.5f), 100f);
            htone.type = Image.Type.Filled; htone.fillMethod = Image.FillMethod.Horizontal;
            htone.fillOrigin = 1; htone.fillAmount = 0f;             // 从右往左揭开（分镜：inset 的左内缩从 100% 收回）
        }
        else htone.color = Paper;

        // 左边纸色小页（分镜 #slabel）
        slabelRt = Rect("SLabel", root, new Vector2(120f, -760f), new Vector2(402f, 240f));
        slabelRt.gameObject.AddComponent<Image>().color = Paper;
        slabelGroup = slabelRt.gameObject.AddComponent<CanvasGroup>(); slabelGroup.alpha = 0f;
        Txt(slabelRt, "N°07 · 系统页", UiFont, 12f, new Vector2(24f, -24f), new Vector2(300f, 16f), TextAlignmentOptions.TopLeft, Cobalt, 9f);
        slT = Txt(slabelRt, "", LabelFont != null ? LabelFont : UiFont, 72f, new Vector2(24f, -52f), new Vector2(354f, 84f), TextAlignmentOptions.TopLeft, Cobalt, 0f);
        slE = Txt(slabelRt, "", UiFont, 15f, new Vector2(24f, -152f), new Vector2(354f, 22f), TextAlignmentOptions.TopLeft, Cobalt, 0f);
        slM = Txt(slabelRt, "", UiFont, 12f, new Vector2(24f, -200f), new Vector2(354f, 16f), TextAlignmentOptions.TopLeft, Cobalt, 9f);

        // 钴蓝面板：从右边滑进来（宽 1244 = 第 5–12 栏；贴画布右缘、高度随画布，宽屏上不留缝）
        panel = Rect("Panel", root, Vector2.zero, new Vector2(1244f, 0f));
        panel.anchorMin = new Vector2(1f, 0f); panel.anchorMax = new Vector2(1f, 1f); panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = Vector2.zero;
        panel.gameObject.AddComponent<Image>().color = Cobalt;

        // 页眉
        Txt(panel, "第 7 幕 · 系统页 · System", UiFont, 12f, new Vector2(24f, -48f), new Vector2(600f, 16f), TextAlignmentOptions.TopLeft, Paper, 9f);
        var closeBtn = Rect("Close", panel, new Vector2(24f + 1112f - 220f, -40f), new Vector2(220f, 32f));
        var cbImg = closeBtn.gameObject.AddComponent<Image>(); cbImg.color = new Color(0, 0, 0, 0);
        Txt(closeBtn, "返回 · Esc", UiFont, 12f, new Vector2(0f, -8f), new Vector2(220f, 16f), TextAlignmentOptions.TopRight, Paper, 9f);
        closeBtn.gameObject.AddComponent<Button>().onClick.AddListener(Close);

        // 四个标签（01 存档 / 02 读档 / 03 回想 / 04 设置）
        for (int i = 0; i < 4; i++)
        {
            int k = i;
            var cell = Rect("Tab" + (i + 1), panel, new Vector2(24f + i * 284f, -88f), new Vector2(260f, 44f));
            cell.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            var cellBg = NewImage("Bg", cell, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            cellBg.color = new Color(0, 0, 0, 0);
            var num = Txt(cell, "0" + (i + 1), UiFont, 12f, new Vector2(14f, -22f), new Vector2(28f, 16f), TextAlignmentOptions.MidlineLeft, Paper, 5f);
            var zh = Txt(cell, TabInfo[i][0], LabelFont != null ? LabelFont : UiFont, 18f, new Vector2(48f, -22f), new Vector2(120f, 24f), TextAlignmentOptions.MidlineLeft, Paper, 0f);
            var en = Txt(cell, TabInfo[i][1].Split(' ')[0].ToUpper(), UiFont, 11f, new Vector2(260f - 14f - 90f, -22f), new Vector2(90f, 16f), TextAlignmentOptions.MidlineRight, Paper, 8f);
            var ln = Rect("Rule", cell, new Vector2(0f, -43f), new Vector2(260f, 1f));
            ln.gameObject.AddComponent<Image>().color = new Color(Paper.r, Paper.g, Paper.b, 0.45f);
            var btn = cell.gameObject.AddComponent<Button>(); btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => { if (tab != TabIds[k] && (k != 0 || inGame)) SetTab(TabIds[k]); });
            tabCells.Add(cell); tabZhs.Add(zh); tabEns.Add(en); tabNums.Add(num); tabLines.Add(ln.GetComponent<Image>());
        }

        // 纸白版面（向右出血到面板右缘）——必须是面板的子物体：面板滑动时它要跟着走，不能留在原地
        sheet = Rect("Sheet", panel, new Vector2(12f, -152f), new Vector2(1232f, 848f));
        sheet.gameObject.AddComponent<Image>().color = Paper;
        sheet.gameObject.AddComponent<RectMask2D>();

        // 脚注
        Txt(panel, "1–4 切换 · 方向键选择 · Enter 确认 · Esc 返回", UiFont, 12f, new Vector2(24f, -1028f), new Vector2(800f, 16f), TextAlignmentOptions.TopLeft, Paper, 9f);
        Txt(panel, "p. 007", UiFont, 12f, new Vector2(24f + 1112f - 200f, -1028f), new Vector2(200f, 16f), TextAlignmentOptions.TopRight, Paper, 9f);

        // 各页共用的「页眉行」
        RefreshTabVisuals();
        RefreshLabel();
    }

    void RefreshTabVisuals()
    {
        for (int i = 0; i < tabCells.Count; i++)
        {
            bool on = TabIds[i] == tab, dis = TabIds[i] == "save" && !inGame;
            var bg = tabCells[i].Find("Bg").GetComponent<Image>();
            bg.color = on ? Paper : new Color(0, 0, 0, 0);
            Color c = on ? Cobalt : new Color(Paper.r, Paper.g, Paper.b, dis ? 0.35f : 1f);
            tabZhs[i].color = c; tabNums[i].color = c;
            tabEns[i].color = new Color(c.r, c.g, c.b, on ? 1f : 0.8f);
            tabLines[i].enabled = !on;
            tabCells[i].GetComponent<Button>().interactable = !dis;
        }
    }

    void RefreshLabel()
    {
        int idx = inGame && dm != null ? dm.CurrentIndex : -1;
        slT.text = TabInfo[System.Array.IndexOf(TabIds, tab)][0];
        slE.text = TabInfo[System.Array.IndexOf(TabIds, tab)][1];
        if (inGame && idx >= 0 && idx < rowInfo.Length)
        {
            var r = rowInfo[idx];
            slM.text = "现在 · 第" + ChapterNums[r.chap] + "章 " + r.chap + "." + r.serial.ToString("00");
        }
        else slM.text = "从标题打开";
    }

    // ───────── 切页 ─────────
    void SetTab(string t) { Rebuild(t, true); }
    void RefreshPage() { Rebuild(tab, false); }                              // 值变了就地重画的场合（不响翻页声）

    void Rebuild(string t, bool sound)
    {
        tab = t;
        RefreshTabVisuals(); RefreshLabel();
        foreach (Transform c in sheet) Destroy(c.gameObject);
        slotCells.Clear(); slotRings.Clear(); slotThumbMasks.Clear(); logPageRts.Clear(); setRows.Clear();
        if (t == "save" || t == "load") BuildSlotsPage();
        else if (t == "log") BuildLogPage();
        else BuildSettingsPage();
        if (sound) PlaySfx("Fly/sfx_page");
    }

    // ───────── 存档 / 读档 ─────────
    void BuildSlotsPage()
    {
        ReadSlots();                                                 // 每次重画都重新读：写档后格子才看得到新内容
        string left = tab == "save" ? "存档 · 点一个档位，把这一页写进去" : "读档 · 点一个档位，从那里继续读";
        Header(left, "档位 01–12");
        for (int i = 0; i < SaveSystem.SlotCount; i++) BuildSlot(i);
        RefreshSlotRing();
    }

    TextMeshProUGUI headRight;                     // 页眉右边那截（回想页要用来显示 p. X / Y）
    void Header(string left, string right)
    {
        Txt(sheet, left, UiFont, 12f, new Vector2(0f, -24f), new Vector2(800f, 16f), TextAlignmentOptions.TopLeft, Cobalt, 9f);
        headRight = Txt(sheet, right, UiFont, 12f, new Vector2(0f, -24f), new Vector2(1112f, 16f), TextAlignmentOptions.TopRight, Cobalt, 9f);
        var ln = Rect("HLine", sheet, new Vector2(0f, -52f), new Vector2(1112f, 1f));
        ln.gameObject.AddComponent<Image>().color = new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.35f);
    }

    void BuildSlot(int i)
    {
        SlotData s = slots[i];
        float x = i % 4 * 284f, y = 80f + i / 4 * 254f;
        var cell = Rect("Slot" + i, sheet, new Vector2(x, -y), new Vector2(260f, 230f));
        var hit = cell.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0);

        // 缩略图（260×146，外边一圈细线；写了档会有一次从左往右的「打印」）
        var thumb = Rect("Thumb", cell, Vector2.zero, new Vector2(260f, 146f));
        thumb.gameObject.AddComponent<Image>().color = Paper;
        var mask = thumb.gameObject.AddComponent<RectMask2D>();
        mask.enabled = false;                                            // 平时不裁；写档动画时打开
        // 图和边框都钉死 260×146：写档时遮罩收放裁掉它们，而不是把图压扁（分镜是 clipPath 擦拭）
        var pic = NewImage("Pic", thumb, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(260f, 146f));
        pic.rectTransform.pivot = new Vector2(0f, 1f);
        if (s.has && s.row < lines.Count)
        {
            var spr = FindSprite(rowInfo[s.row].bg);
            if (spr != null) pic.sprite = spr;
        }
        else pic.color = new Color(Paper.r - 0.03f, Paper.g - 0.03f, Paper.b - 0.03f, 1f);
        var frame = NewImage("Frame", thumb, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(260f, 146f));
        frame.rectTransform.pivot = new Vector2(0f, 1f);
        frame.sprite = FrameSprite(); frame.type = Image.Type.Sliced; frame.color = new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.35f);
        if (s.has && s.auto)                                              // 「折角」自动档的耳朵
        {
            var ear = Rect("Ear", thumb, new Vector2(0f, -118f), new Vector2(28f, 28f));
            var earImg = ear.gameObject.AddComponent<Image>();
            earImg.raycastTarget = false; earImg.sprite = EarSprite();
        }

        if (s.has && s.row < lines.Count)
        {
            var r = rowInfo[s.row];
            string no = "N°" + (i + 1).ToString("00");
            Txt(cell, no + " · " + r.chap + "." + r.serial.ToString("00"), UiFont, 11f, new Vector2(0f, -160f), new Vector2(200f, 14f), TextAlignmentOptions.TopLeft, Cobalt, 5f);
            if (s.auto) Txt(cell, "折角 · 自动", UiFont, 11f, new Vector2(0f, -160f), new Vector2(260f, 14f), TextAlignmentOptions.TopRight, Cobalt, 5f);
            Txt(cell, "第" + ChapterNums[r.chap] + "章 · " + ChapterTitles[Mathf.Clamp(r.chap, 1, 5)], UiFont, 14f, new Vector2(0f, -178f), new Vector2(260f, 20f), TextAlignmentOptions.TopLeft, Cobalt, 0f);
            var t3 = Txt(cell, Clean(lines[s.row].text), TextFont != null ? TextFont : UiFont, 13f, new Vector2(0f, -198f), new Vector2(260f, 20f), TextAlignmentOptions.TopLeft, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.75f), 0f);
            t3.overflowMode = TextOverflowModes.Ellipsis;
            Txt(cell, s.date, UiFont, 11f, new Vector2(0f, -218f), new Vector2(260f, 16f), TextAlignmentOptions.TopLeft, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.6f), 0f);
        }
        else
        {
            var cross = Rect("Cross", cell, new Vector2(82f, -25f), new Vector2(96f, 96f));
            var crossImg = cross.gameObject.AddComponent<Image>();
            crossImg.raycastTarget = false; crossImg.sprite = RegSprite(); crossImg.color = new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.55f);
            Txt(cell, "N°" + (i + 1).ToString("00"), UiFont, 11f, new Vector2(0f, -160f), new Vector2(200f, 14f), TextAlignmentOptions.TopLeft, Cobalt, 5f);
            Txt(cell, "空", UiFont, 11f, new Vector2(0f, -160f), new Vector2(260f, 14f), TextAlignmentOptions.TopRight, Cobalt, 5f);
        }

        // 选中：3px 钴蓝内框
        var ring = Rect("Ring", cell, Vector2.zero, new Vector2(260f, 146f));
        Bar(ring, new Vector2(0f, 0f), new Vector2(260f, 3f));
        Bar(ring, new Vector2(0f, -143f), new Vector2(260f, 3f));
        Bar(ring, new Vector2(0f, -3f), new Vector2(3f, 140f));
        Bar(ring, new Vector2(257f, -3f), new Vector2(3f, 140f));
        ring.gameObject.SetActive(false);

        int k = i;
        var trig = cell.gameObject.AddComponent<EventTrigger>();
        AddEntry(trig, EventTriggerType.PointerEnter, () => SelectSlot(k));
        AddEntry(trig, EventTriggerType.PointerClick, () => { SelectSlot(k); SlotAct(k); });

        slotCells.Add(cell); slotRings.Add(ring); slotThumbMasks.Add(thumb);
    }

    void Bar(RectTransform parent, Vector2 pos, Vector2 size)
    {
        var b = Rect("Bar", parent, pos, size);
        b.gameObject.AddComponent<Image>().color = Cobalt;
    }

    void SelectSlot(int i)
    {
        if (slotSel == i) return;
        slotSel = i; PlaySfx("Fly/sfx_tick"); RefreshSlotRing();
    }
    void RefreshSlotRing()
    {
        for (int i = 0; i < slotRings.Count; i++) slotRings[i].gameObject.SetActive(i == slotSel);
    }

    void SlotAct(int i)
    {
        ReadSlots();
        if (tab == "save")
        {
            if (!inGame || dm == null || dm.CurrentIndex < 0) return;
            SaveSystem.WriteSlot(i, dm.CurrentIndex, false);
            PlaySfx("Fly/sfx_stamp");
            int keep = slotSel;
            RefreshPage();
            slotSel = keep; RefreshSlotRing();
            // 打印动画：这一格的缩略图从左往右「印」出来
            var thumb = slotThumbMasks[i];
            thumb.GetComponent<RectMask2D>().enabled = true;
            thumb.sizeDelta = new Vector2(0f, 146f);
            StartCoroutine(PrintSlot(thumb));
        }
        else if (tab == "load" && slots[i].has)
        {
            LoadSlot(i);
        }
    }

    IEnumerator PrintSlot(RectTransform thumb)
    {
        // 打印动画期间如果翻了页 / 重建了格子，这个缩略图会被销毁：每帧先问一句还在不在
        yield return Tweens.Tween(0.38f, Tweens.EaseOut, p => { if (thumb != null) thumb.sizeDelta = new Vector2(260f * p, 146f); });
        if (thumb != null)
        {
            var mask = thumb.GetComponent<RectMask2D>();
            if (mask != null) mask.enabled = false;
            thumb.sizeDelta = new Vector2(260f, 146f);
        }
    }

    void LoadSlot(int i)
    {
        PlaySfx("Fly/sfx_select"); PlaySfx("Fly/sfx_whoosh");
        StartCoroutine(LoadRoutine(slots[i].row, slotThumbMasks[i]));   // 从「缩略图」那块开始涨（分镜：260×146 卡在图边上）
    }

    IEnumerator LoadRoutine(int row, RectTransform thumb)
    {
        // T4：一块钴蓝从这张缩略图的位置展开盖满，然后在后面换场景，最后 12 栏抽离露出来（分镜 loadSlot）
        Vector3[] c = new Vector3[4]; thumb.GetWorldCorners(c);
        Vector2 tl = root.InverseTransformPoint(c[1]), br = root.InverseTransformPoint(c[3]);
        float cw = root.rect.width, ch = root.rect.height;
        float x = tl.x + cw * 0.5f, y = ch * 0.5f - tl.y, w = br.x - tl.x, h = tl.y - br.y;

        var fgo = new GameObject("LoadFlood", typeof(RectTransform), typeof(Image));
        var fr = (RectTransform)fgo.transform; fr.SetParent(root, false);
        fr.anchorMin = fr.anchorMax = new Vector2(0f, 1f); fr.pivot = new Vector2(0f, 1f);
        fgo.GetComponent<Image>().color = Cobalt;
        fr.SetAsLastSibling();
        SetF(fr, x, y, w, h);
        for (float t = 0f; t < 0.46f; t += Time.unscaledDeltaTime)
        {
            if (t < 0.24f) { float e = EaseInOut(t / 0.24f); float l = Mathf.Lerp(x, 0f, e), r = Mathf.Lerp(x + w, cw, e); SetF(fr, l, y, r - l, h); }
            else { float e = EaseInOut((t - 0.24f) / 0.22f); float tp = Mathf.Lerp(y, 0f, e), bt = Mathf.Lerp(y + h, ch, e); SetF(fr, 0f, tp, cw, bt - tp); }
            yield return null;
        }
        SetF(fr, 0f, 0f, cw, ch);

        if (inGame && dm != null)
        {
            dm.JumpTo(row);
            var cc = FindObjectOfType<ChapterCard>();
            if (cc != null)
            {
                cc.SetColumnsNow(1f);                                    // 先盖住（画面本来就是钴蓝）
                DiscardVisuals();                                        // 不能 Destroy：这个协程还挂在身上
                // 不等待：同一拍里就接着抽离（分镜 revealFromCard 是无缝连着的）
                yield return Tweens.Tween(cc.ColumnsTotal, t => t, p => cc.SetColumnsAt(p * cc.ColumnsTotal, false));
                Destroy(gameObject);
            }
            else CloseNow();
        }
        else
        {
            SaveSystem.PendingRow = row;
            ChapterCard.CoveredEntry = true;                             // 游戏场景从「已盖住」开始，EntryLift 会抽离
            closing = true; UiOpen = Mathf.Max(0, UiOpen - 1);
            SceneManager.LoadScene("SampleScene");
        }
    }

    void SetF(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    // ───────── 回想 ─────────
    void BuildLogPage()
    {
        int upto = -1;
        if (inGame && dm != null) upto = dm.CurrentIndex;
        else if (SaveSystem.SavedRow >= 0) upto = SaveSystem.SavedRow;
        var rows = BuildLogRows(upto);
        string head = rows.Count > 0
            ? "回想 · 读过的 " + rows.Count + " 句" + (inGame ? " · 点一句回到那里" : "")
            : "回想 · 上次读到折角处";
        Header(head, "p. 1 / 1");

        // 两个竖栏、每栏 544 宽；放不下就翻页（分镜：column-width 544 / gap 24）
        const float ColW = 544f, TextW = 488f, Gap = 24f, PageH = 736f;
        float y = 0f; int col = 0;
        RectTransform pageRt = NewLogPage();
        foreach (LogRow e in rows)
        {
            string body = Clean(e.text);
            string s = string.IsNullOrEmpty(e.name) ? body
                : "<b>" + e.name + "</b>" + (e.think ? "<size=12> · 心声</size>" : "") + "　" + body;
            if (e.book) s = "<size=11>书中　</size>" + body;
            float fs = e.book ? 15f : 17f;
            var probe = Txt(sheet, s, TextFont != null ? TextFont : UiFont, fs, Vector2.zero, new Vector2(TextW, 28f), TextAlignmentOptions.TopLeft, Cobalt, 0f);
            probe.enableWordWrapping = true; probe.lineSpacing = e.book ? 26f : 28f;
            float fh = Mathf.Max(28f, probe.GetPreferredValues(s, TextW, 0f).y);
            Destroy(probe.gameObject);
            float need = fh + 14f;
            if (y + need > PageH)                                           // 这一栏放不下了
            {
                if (col == 0) { col = 1; y = 0f; }
                else { col = 0; y = 0f; pageRt = NewLogPage(); }
            }
            float x0 = col * (ColW + Gap);
            Txt(pageRt, e.chap.ToString("00") + e.serial.ToString("00"), UiFont, 11f, new Vector2(x0, -y - 5f), new Vector2(56f, 16f), TextAlignmentOptions.TopLeft, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.6f), 4f);
            var t = Txt(pageRt, s, TextFont != null ? TextFont : UiFont, fs, new Vector2(x0 + 56f, -y), new Vector2(TextW, fh), TextAlignmentOptions.TopLeft, Cobalt, 0f);
            t.enableWordWrapping = true; t.lineSpacing = e.book ? 26f : 28f;
            if (e.book)                                                     // 「书中」：左边一道细线（再缩进 14）
            {
                var line = Rect("BkLine", t.rectTransform, new Vector2(-14f, 0f), new Vector2(1f, fh));
                line.gameObject.AddComponent<Image>().color = Cobalt;
                t.rectTransform.anchoredPosition = new Vector2(x0 + 56f + 14f, -y);
            }
            if (inGame)                                                     // 点一句回到那里
            {
                int r = e.row;
                var hit = Rect("Hit", pageRt, new Vector2(x0, -y), new Vector2(ColW, need));
                var hi = hit.gameObject.AddComponent<Image>(); hi.color = new Color(0, 0, 0, 0);
                var trig = hit.gameObject.AddComponent<EventTrigger>();
                AddEntry(trig, EventTriggerType.PointerClick, () => { CloseNow(); if (dm != null) dm.JumpTo(r); });
            }
            y += need;
        }
        logPages = Mathf.Max(1, logPageRts.Count);
        logPage = logPages - 1;                                             // 从最新的一页开始看
        ShowLogPage();
    }

    RectTransform NewLogPage()
    {
        var r = Rect("Page" + logPageRts.Count, sheet, new Vector2(0f, -88f), new Vector2(1112f, 736f));
        logPageRts.Add(r);
        return r;
    }

    void ShowLogPage()
    {
        for (int i = 0; i < logPageRts.Count; i++) logPageRts[i].gameObject.SetActive(i == logPage);
        if (headRight != null) headRight.text = "p. " + (logPage + 1) + " / " + Mathf.Max(1, logPageRts.Count);
    }

    void FlipLog(int d)
    {
        if (logPageRts.Count == 0) return;
        logPage = Mathf.Clamp(logPage + d, 0, logPageRts.Count - 1);
        ShowLogPage();
        PlaySfx("Fly/sfx_tick");
    }

    List<LogRow> BuildLogRows(int upto)
    {
        var list = new List<LogRow>();
        if (upto < 0) upto = PreviewUpto();
        if (upto < 0 || lines.Count == 0) return list;
        if (upto >= lines.Count) upto = lines.Count - 1;
        int a = lines.FindIndex(l => l.flow == "label:A"), b = lines.FindIndex(l => l.flow == "label:B");
        bool inB = b >= 0 && upto >= b;
        string name = "";
        for (int i = 0; i <= upto; i++)
        {
            var l = lines[i];
            if (inB && a >= 0 && i >= a && i < b) continue;                 // 走的是 B 就跳过 A 那一段
            if (l.speakerName != null) name = l.speakerName;
            if (string.IsNullOrEmpty(l.text) || (l.flow != null && l.flow.StartsWith("option:"))) continue;
            var r = rowInfo[i];
            list.Add(new LogRow
            {
                row = i, chap = r.chap, serial = r.serial, name = name, text = l.text,
                book = l.lineType == "书中", think = l.lineType == "心声"
            });
        }
        return list;
    }

    int PreviewUpto()                                                        // 没存档时：第一章前 8 句（分镜的预览）
    {
        int count = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            if (!string.IsNullOrEmpty(l.text) && (l.flow == null || !l.flow.StartsWith("option:")))
            {
                count++;
                if (count >= 8) return i;
            }
        }
        return lines.Count - 1;
    }

    // ───────── 设置 ─────────
    void BuildSettingsPage()
    {
        Header("设置 · 改动立即生效", "Settings");
        setRows.Clear(); setSel = 0;
        AddSetRow(0, "文字速度", "TEXT SPEED", 0.01f, 0.06f, 0.001f, () => SaveSystem.TextDelay, v => SaveSystem.TextDelay = v, v => Mathf.RoundToInt(v * 1000f) + " ms / 字", null);
        AddSetRow(1, "自动间隔", "AUTO DELAY", 0.5f, 3f, 0.1f, () => SaveSystem.AutoDelay, v => SaveSystem.AutoDelay = v, v => v.ToString("0.0") + " s", null);
        AddSetRow(2, "对话条", "TEXT BOX", 0.4f, 1f, 0.05f, () => SaveSystem.Box, v => SaveSystem.Box = v, v => Mathf.RoundToInt(v * 100f) + " %", null);
        AddSetRow(3, "音乐", "MUSIC", 0f, 1f, 0.05f, () => SaveSystem.Music, v => { SaveSystem.Music = v; ApplyAudio(); }, v => Mathf.RoundToInt(v * 100f) + " %", null);
        AddSetRow(4, "音效", "SOUND", 0f, 1f, 0.05f, () => SaveSystem.Sfx, v => { SaveSystem.Sfx = v; ApplyAudio(); }, v => Mathf.RoundToInt(v * 100f) + " %", null);
        AddSetRow(5, "环境声", "AMBIENCE", 0f, 1f, 0.05f, () => SaveSystem.Ambience, v => { SaveSystem.Ambience = v; ApplyAudio(); }, v => Mathf.RoundToInt(v * 100f) + " %", null);
        AddSetRow(6, "梦境帧率", "DREAM FPS", 0f, 0f, 0f, () => SaveSystem.DreamFps, v => SaveSystem.DreamFps = v, v => Mathf.RoundToInt(v) + " fps", new float[] { 6f, 8f, 12f, 24f });

        // 恢复默认（分镜 sbtn）
        var reset = Rect("Reset", sheet, new Vector2(0f, -648f), new Vector2(120f, 44f));
        var ri = reset.gameObject.AddComponent<Image>(); ri.color = new Color(0, 0, 0, 0);
        var rl = NewImage("Box", reset, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); rl.sprite = FrameSprite(); rl.type = Image.Type.Sliced; rl.color = Cobalt;
        Txt(reset, "恢复默认", UiFont, 16f, new Vector2(20f, -22f), new Vector2(90f, 20f), TextAlignmentOptions.MidlineLeft, Cobalt, 0f);
        reset.gameObject.AddComponent<Button>().onClick.AddListener(() => { PlaySfx("Fly/sfx_select"); SaveSystem.ResetSettings(); ApplyAudio(); SetTab("settings"); });

        // 游戏里：合上书，回到标题
        if (inGame)
        {
            var outBtn = Rect("Exit", sheet, new Vector2(284f, -648f), new Vector2(190f, 44f));
            var oi = outBtn.gameObject.AddComponent<Image>(); oi.color = Cobalt;
            oi.raycastTarget = true;
            Txt(outBtn, "合上书，回到标题", UiFont, 16f, new Vector2(20f, -22f), new Vector2(150f, 20f), TextAlignmentOptions.MidlineLeft, Paper, 0f);
            outBtn.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                PlaySfx("Fly/sfx_select");
                CloseNow();
                var cc = FindObjectOfType<ChapterCard>();
                if (cc != null) cc.ExitToTitle();
                else SceneManager.LoadScene(0);
            });
        }
        RefreshSetSel();
    }

    void ApplyAudio()
    {
        var a = FindObjectOfType<AudioPlayer>();
        if (a != null) a.ApplyVolumes();
    }

    void AddSetRow(int i, string zh, string en, float min, float max, float step, System.Func<float> get, System.Action<float> set, System.Func<float, string> show, float[] opts)
    {
        float y = 80f + i * 76f;
        var row = Rect("Row" + zh, sheet, new Vector2(0f, -y), new Vector2(1112f, 72f));
        var ln = Rect("Line", row, new Vector2(0f, -71f), new Vector2(1112f, 1f));
        ln.gameObject.AddComponent<Image>().color = new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.18f);
        var zhT = Txt(row, zh, UiFont, 18f, new Vector2(0f, -16f), new Vector2(240f, 24f), TextAlignmentOptions.TopLeft, Cobalt, 0f);
        Txt(row, en, UiFont, 11f, new Vector2(0f, -40f), new Vector2(240f, 16f), TextAlignmentOptions.TopLeft, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.7f), 8f);
        var val = Txt(row, "", UiFont, 20f, new Vector2(1112f - 200f, -22f), new Vector2(200f, 24f), TextAlignmentOptions.TopRight, Cobalt, 0f);

        var sr = new SetRow { zh = zh, en = en, min = min, max = max, step = step, get = get, set = set, show = show, opts = opts, val = val, zhT = zhT };

        if (opts != null)                                                // 段选按钮（梦境帧率）
        {
            float bw = 686f / opts.Length;
            for (int k = 0; k < opts.Length; k++)
            {
                float bxx = 284f + k * bw;
                var b = Rect("Seg" + k, row, new Vector2(bxx, -16f), new Vector2(bw - 2f, 40f));
                var bi = b.gameObject.AddComponent<Image>();
                bi.sprite = FrameSprite(); bi.type = Image.Type.Sliced;
                bool on = Mathf.Abs(get() - opts[k]) < 0.01f;
                bi.color = on ? Cobalt : new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.5f);
                var bt = Txt(b, show(opts[k]), UiFont, 13f, Vector2.zero, new Vector2(bw - 2f, 40f), TextAlignmentOptions.Center, on ? Paper : Cobalt, 4f);
                float v = opts[k];
                b.gameObject.AddComponent<Button>().onClick.AddListener(() => { set(v); PlaySfx("Fly/sfx_select"); RefreshPage(); });
            }
        }
        else
        {
            // 滑杆：686 宽，线在中间，方块滑块
            var tr = Rect("Track", row, new Vector2(284f, -36f), new Vector2(686f, 32f));
            var ti = tr.gameObject.AddComponent<Image>(); ti.color = new Color(0, 0, 0, 0);
            var tl = Rect("TLine", tr, new Vector2(0f, 16f), new Vector2(686f, 1f));
            tl.gameObject.AddComponent<Image>().color = Cobalt;
            Txt(row, min.ToString("0.##"), UiFont, 11f, new Vector2(284f, -54f), new Vector2(60f, 14f), TextAlignmentOptions.TopLeft, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.6f), 0f);
            Txt(row, max.ToString("0.##"), UiFont, 11f, new Vector2(284f + 686f - 60f, -54f), new Vector2(60f, 14f), TextAlignmentOptions.TopRight, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.6f), 0f);
            var kn = Rect("Knob", tr, new Vector2(0f, 8f), new Vector2(16f, 16f));
            kn.gameObject.AddComponent<Image>().color = Cobalt;
            sr.knob = kn;

            System.Action refresh = () =>
            {
                float u = max > min ? Mathf.Clamp01((get() - min) / (max - min)) : 0f;
                kn.anchoredPosition = new Vector2(686f * u - 8f, 8f);
                val.text = show(get());
            };
            sr.refresh = refresh;
            refresh();

            var trig = tr.gameObject.AddComponent<EventTrigger>();
            UnityEngine.Events.UnityAction<BaseEventData> at = d =>
            {
                var pd = (PointerEventData)d;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(tr, pd.position, pd.pressEventCamera, out Vector2 lp);
                float u = Mathf.Clamp01(lp.x / 686f);                    // 轴心在左上，lp.x 就是 0~686
                float nv = Mathf.Clamp(Mathf.Round(Mathf.Lerp(min, max, u) / step) * step, min, max);
                if (Mathf.Abs(nv - get()) > 0.0001f) { set(nv); PlaySfx("Fly/sfx_tick"); refresh(); }
            };
            foreach (var type in new[] { EventTriggerType.PointerDown, EventTriggerType.Drag })
            { var e = new EventTrigger.Entry { eventID = type }; e.callback.AddListener(at); trig.triggers.Add(e); }
        }
        setRows.Add(sr);
    }

    // 键盘选中的行：中文标题亮一档，别的压暗（拖动时用它自己的 refresh，不在这里动）
    void RefreshSetSel()
    {
        for (int i = 0; i < setRows.Count; i++)
        {
            var c = Cobalt; c.a = i == setSel ? 1f : 0.45f;
            if (setRows[i].zhT != null) setRows[i].zhT.color = c;
        }
    }

    void AdjustSetting(int d)
    {
        if (setSel < 0 || setSel >= setRows.Count) return;
        var r = setRows[setSel];
        if (r.opts != null)
        {
            int idx = System.Array.FindIndex(r.opts, o => Mathf.Abs(o - r.get()) < 0.01f);
            if (idx < 0) idx = 0;
            idx = Mathf.Clamp(idx + d, 0, r.opts.Length - 1);
            r.set(r.opts[idx]);
            PlaySfx("Fly/sfx_tick"); RefreshPage();
        }
        else
        {
            float nv = Mathf.Clamp(r.get() + d * Mathf.Max(r.step, 0.001f), r.min, r.max);
            if (Mathf.Abs(nv - r.get()) > 0.0001f) { r.set(nv); PlaySfx("Fly/sfx_tick"); if (r.refresh != null) r.refresh(); }
        }
    }

    // ───────── 每帧：进出场动画 + 键盘 ─────────
    void Update()
    {
        if (panel == null) return;                                      // 界面还没建好（OpenFlow 采样那一两帧）
        if (phaseStart < 0f) phaseStart = Time.realtimeSinceStartup;    // 这个阶段的第一帧才开始计时
        float t = Time.realtimeSinceStartup - phaseStart;
        if (phase == Phase.Opening)                                     // 分镜 openSys：面板先滑（72% 到位）、网格随后揭开、纸页最后淡入
        {
            float u = Mathf.Clamp01(t / 0.42f);
            SetPanelX(1244f * (1f - Tweens.EaseOut(Mathf.Clamp01(u / 0.72f))));
            if (htone.sprite != null) htone.fillAmount = Tweens.EaseOut(Mathf.Clamp01((u - 0.1f) / 0.9f));
            slabelGroup.alpha = Mathf.Clamp01((u - 0.55f) / 0.45f);
            if (t >= 0.42f) { phase = Phase.Open; SetPanelX(0f); if (htone.sprite != null) htone.fillAmount = 1f; slabelGroup.alpha = 1f; }
            return;
        }
        if (phase == Phase.Closing)                                     // 分镜 closeSys：面板先走、网格随后收
        {
            float u = Mathf.Clamp01(t / 0.3f);
            SetPanelX(1244f * (u * u * u));
            if (htone.sprite != null) htone.fillAmount = 1f - Mathf.Pow(Mathf.Clamp01(u / 0.83f), 3f);
            slabelGroup.alpha = 1f - Mathf.Clamp01(u / 0.3f);
            if (t >= 0.3f) Destroy(gameObject);
            return;
        }

        // ── 键盘（分镜 sysKey）──
        if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
        for (int k = 0; k < 4; k++)
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + k)) && (k != 0 || inGame)) { if (tab != TabIds[k]) SetTab(TabIds[k]); return; }

        if (tab == "log")
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow)) FlipLog(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) FlipLog(1);
        }
        else if (tab == "settings")
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) && setSel > 0) { setSel--; PlaySfx("Fly/sfx_tick"); RefreshSetSel(); }
            if (Input.GetKeyDown(KeyCode.DownArrow) && setSel < setRows.Count - 1) { setSel++; PlaySfx("Fly/sfx_tick"); RefreshSetSel(); }
            if (Input.GetKeyDown(KeyCode.LeftArrow)) AdjustSetting(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) AdjustSetting(1);
        }
        else
        {
            int mv = 0;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) mv = -1;
            if (Input.GetKeyDown(KeyCode.RightArrow)) mv = 1;
            if (Input.GetKeyDown(KeyCode.UpArrow)) mv = -4;
            if (Input.GetKeyDown(KeyCode.DownArrow)) mv = 4;
            if (mv != 0) { slotSel = Mathf.Clamp(slotSel < 0 ? 0 : slotSel + mv, 0, SaveSystem.SlotCount - 1); PlaySfx("Fly/sfx_tick"); RefreshSlotRing(); }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            { if (slotSel >= 0) SlotAct(slotSel); }
        }
    }

    void SetPanelX(float off) { panel.anchoredPosition = new Vector2(off, 0f); }   // 轴心在右上：off=1244 在画外，0 = 贴右缘

    // ───────── 小工具 ─────────
    void AddEntry(EventTrigger trig, EventTriggerType type, System.Action a)
    {
        var e = new EventTrigger.Entry { eventID = type };
        e.callback.AddListener(_ => a());
        trig.triggers.Add(e);
    }

    static string Clean(string s) { return s == null ? "" : s.Replace("{nw}", "").Replace("{w}", ""); }

    static Canvas FindTopCanvas()
    {
        foreach (var c in FindObjectsOfType<Canvas>()) if (c.isRootCanvas) return c;
        return FindObjectOfType<Canvas>();
    }

    void PlaySfx(string n)
    {
        var a = FindObjectOfType<AudioPlayer>();
        if (a != null) { a.PlaySfx(n); return; }
        var c = Resources.Load<AudioClip>("Audio/" + n);
        if (c != null) AudioSource.PlayClipAtPoint(c, Camera.main ? Camera.main.transform.position : Vector3.zero, SaveSystem.Sfx);
    }

    static RectTransform Rect(string n, RectTransform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(n, typeof(RectTransform));
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    static Image NewImage(string n, RectTransform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.GetComponent<Image>(); img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI Txt(RectTransform parent, string s, TMP_FontAsset f, float size, Vector2 pos, Vector2 box, TextAlignmentOptions al, Color c, float spacing)
    {
        var go = new GameObject("T", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos; rt.sizeDelta = box;
        var t = go.GetComponent<TextMeshProUGUI>(); if (f != null) t.font = f;
        t.text = s; t.fontSize = size; t.color = c; t.alignment = al; t.characterSpacing = spacing;
        t.enableWordWrapping = false; t.raycastTarget = false;
        return t;
    }

    static float EaseInOut(float x) { return x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f; }

    // ───────── 贴图小零件（只生成一次）─────────
    static Sprite frameSprite;
    static Sprite FrameSprite()      // 3×3 细框，九宫格拉伸
    {
        if (frameSprite != null) return frameSprite;
        var tex = new Texture2D(3, 3) { filterMode = FilterMode.Point };
        for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++) tex.SetPixel(x, y, x == 1 && y == 1 ? Color.clear : Color.white);
        tex.Apply();
        return frameSprite = Sprite.Create(tex, new Rect(0, 0, 3, 3), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(1, 1, 1, 1));
    }

    static Sprite earSprite;
    static Sprite EarSprite()        // 「折角」的小三角
    {
        if (earSprite != null) return earSprite;
        int n = 56;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            // HTML：polygon 0,0 0,28 28,28（纸色）+ 0,0 28,28 28,0（亮纸）+ 描边
            float u = (x + 0.5f) / n * 28f, v = (y + 0.5f) / n * 28f;    // 贴图 y 向上 → 这里的 v 是从下往上
            float yDown = 28f - v;
            Color c = Color.clear;
            if (x <= n - 1 && y <= n - 1)
            {
                bool lower = yDown >= u;                                  // 下半：0,0-0,28-28,28
                bool upper = !lower;                                      // 上半：0,0-28,28-28,0
                float d = Mathf.Abs(u - yDown);
                if (d < 1.2f || u < 1f || yDown > 27f) c = new Color(0f, 0.278f, 0.671f);       // 描边钴蓝
                else if (lower) c = new Color(0.847f, 0.812f, 0.737f);    // #D8CFBC
                else c = new Color(0.914f, 0.882f, 0.812f);               // #E9E1CF
            }
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return earSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite regSprite;
    static Sprite RegSprite()        // 空档位的套准十字（圈 + 十字）
    {
        if (regSprite != null) return regSprite;
        int n = 96; float c = (n - 1) * 0.5f;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float dx = x - c, dy = y - c, d = Mathf.Sqrt(dx * dx + dy * dy);
            bool on = Mathf.Abs(d - 10f) < 1.1f                       // 圈
                   || (Mathf.Abs(dy) < 1f && Mathf.Abs(dx) < 24f)     // 横线
                   || (Mathf.Abs(dx) < 1f && Mathf.Abs(dy) < 24f);    // 竖线
            tex.SetPixel(x, y, on ? new Color(0f, 0.278f, 0.671f, 0.55f) : Color.clear);
        }
        tex.Apply();
        return regSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    // ───────── 背景网点（分镜 renderHalftone）─────────
    static Texture2D MakeHalftone()
    {
        Texture2D src = null;
        try { src = ScreenCapture.CaptureScreenshotAsTexture(); } catch { }
        if (src == null) return null;
        const int SW = 320, SH = 180;
        var lum = new float[SW * SH];
        for (int y = 0; y < SH; y++)
            for (int x = 0; x < SW; x++)
            {
                // 注意：截图纹理（CaptureScreenshotAsTexture）的 v 方向和常规纹理相反——v=0 才是屏幕顶部，
                // 所以这里不加 1-v 的翻转（加过，结果整张印反了：草坡跑到上面、天空堆在下面）
                Color c = src.GetPixelBilinear((x + 0.5f) / SW, (y + 0.5f) / SH);
                lum[y * SW + x] = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
            }
        Destroy(src);
        int W = 1920, H = 1080, cell = 14;
        var px = new Color32[W * H];
        Color32 paper = Paper, cobalt = Cobalt;
        for (int i = 0; i < px.Length; i++) px[i] = paper;
        float ang = Mathf.PI / 12f, ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
        int N = Mathf.CeilToInt(Mathf.Sqrt(W * W + H * H) / cell * 0.5f) + 1;
        for (int j = -N; j <= N; j++)
            for (int i = -N; i <= N; i++)
            {
                float pxf = 960f + (i * ca - j * sa) * cell, pyf = 540f + (i * sa + j * ca) * cell;   // 15° 网角
                if (pxf < -cell || pyf < -cell || pxf > W + cell || pyf > H + cell) continue;
                int sxi = Mathf.Clamp((int)(pxf / 6f), 0, SW - 1), syi = Mathf.Clamp((int)(pyf / 6f), 0, SH - 1);
                float L = lum[syi * SW + sxi];
                float dk = Mathf.Pow(Mathf.Clamp01((0.93f - L) / 0.93f * 1.4f), 1.3f);
                if (dk < 0.03f) continue;
                float r = cell * 0.64f * Mathf.Sqrt(dk);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(pxf - r)), x1 = Mathf.Min(W - 1, Mathf.CeilToInt(pxf + r));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(pyf - r)), y1 = Mathf.Min(H - 1, Mathf.CeilToInt(pyf + r));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x + 0.5f - pxf, dy = y + 0.5f - pyf;
                        if (dx * dx + dy * dy <= r * r) px[(H - 1 - y) * W + x] = cobalt;
                    }
            }
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px); tex.Apply();
        return tex;
    }

    // ───────── 图片索引（复刻 DialogueManager 的查法）─────────
    static Dictionary<string, string> spriteIndex;
    static Sprite FindSprite(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (spriteIndex == null)
        {
            spriteIndex = new Dictionary<string, string>();
            var file = Resources.Load<TextAsset>("SpriteIndex");
            if (file != null)
                foreach (string row in file.text.Split('\n'))
                {
                    string[] parts = row.TrimEnd('\r').Split('\t');
                    if (parts.Length >= 2) spriteIndex[parts[0]] = parts[1];
                }
        }
        Sprite s = Resources.Load<Sprite>(name + "_base");
        if (s != null) return s;
        s = Resources.Load<Sprite>(name);
        if (s != null) return s;
        if (spriteIndex.TryGetValue(name + "_base", out string p1)) { s = Resources.Load<Sprite>(p1); if (s != null) return s; }
        if (spriteIndex.TryGetValue(name, out string p2)) return Resources.Load<Sprite>(p2);
        return null;
    }
}
