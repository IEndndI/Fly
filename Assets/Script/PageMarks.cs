using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 照第一版分镜（index_v1）补的两样页面小零件，GameChrome 在 Awake 里自己挂上：
// ① 折角：读到「Cus 恋恋不舍地在书的左下角轻轻折上一个角」那句，画面左下角真的折下一个角（0.52 秒），
//    旁边印「折角 · 已自动存档 · 1.NN」；这一章结束（下一个 label）时淡掉。读档落在折角之后：直接是折好的。
// ② 码数牌：两次起飞时右上角的钴蓝方块，数字跟着打字一格格减（九码、八码……）。
//    第二章最后一码离地时往上飞走；第五章卡在某一码抖一下，打完字掉下去。结局 B 换成「步 · Steps」往上飞。
public class PageMarks : MonoBehaviour
{
    static readonly Color Cobalt = new Color(0f, 0.278f, 0.671f);
    static readonly Color Paper = new Color(0.949f, 0.925f, 0.875f);

    // 每句（按句子里的一段文字认）：s = 随打字进度依次显示的数字；sv = 卡住的那个数；out −1 飞走 / +1 掉下；atStart = 一开始就飞走
    class Yard { public string key; public int[] s; public int sv = -99; public int outDir; public bool atStart; public string lab; }
    static readonly Yard[] Yards = {
        new Yard { key = "没有倒数，没有仪式", s = new[] { 9, 9, 9, 9, 8 } },
        new Yard { key = "旋转的扇叶搅动地上的草", s = new[] { 5, 4, 3, 2, 1, 0 } },
        new Yard { key = "他离开了地面，起初只是悬浮", s = new[] { 0 }, outDir = -1, atStart = true },
        new Yard { key = "风向变了，从背后推", s = new[] { 9, 9, 9, 8, 8, 8, 7, 7, 7, 7, 3, 2 }, sv = 7 },
        new Yard { key = "他拼命地拉起操纵杆", s = new[] { 1, 1, 1, 1 }, sv = 1, outDir = 1 },
        new Yard { key = "再试。推得更猛", s = new[] { 3, 3, 3, 2, 2, 2, 2, 2 }, sv = 2, outDir = 1 },
        new Yard { key = "他张开双臂，将手臂伸展到机翼的弧度", s = new[] { 1, 1, 1, 3, 3, 5, 5, 6, 6, 6 }, outDir = -1, lab = "步 · Steps" },
    };
    const string FoldKey = "左下角轻轻折上一个角";

    DialogueManager dm; AudioPlayer audioP; TMP_FontAsset font;
    Tri hole, flap; TextMeshProUGUI saveText; CanvasGroup foldGroup;
    RectTransform yard; CanvasGroup yardGroup; TextMeshProUGUI yardLab, yardNum;
    int foldRow = -1, foldEnd = -1, foldNo, lastIndex = -2;
    float foldT0; int foldState;                 // 0 没折 · 1 折着 · 2 正在淡掉
    Yard curYard; int yardK = -1, yardN = -99; float lineT0, doneT = -1f, stallT = -9f;

    public void Init(DialogueManager d, TMP_FontAsset f)
    {
        dm = d; font = f; audioP = FindObjectOfType<AudioPlayer>();
        var root = (RectTransform)transform;
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;

        // 折角：钉在左下角（AE 坐标 (0,968)-(112,1080) 那个三角）
        var fr = new GameObject("Fold", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        fr.SetParent(root, false); fr.anchorMin = fr.anchorMax = fr.pivot = Vector2.zero; fr.anchoredPosition = Vector2.zero; fr.sizeDelta = new Vector2(600, 112);
        foldGroup = fr.GetComponent<CanvasGroup>(); foldGroup.alpha = 0f; foldGroup.blocksRaycasts = false;
        hole = MakeTri(fr, "Hole", new Color(0.847f, 0.812f, 0.737f)); hole.Set(new Vector2(0, 112), Vector2.zero, new Vector2(112, 0));
        flap = MakeTri(fr, "Flap", new Color(0.914f, 0.882f, 0.812f)); flap.stroke = 2f; flap.strokeColor = Cobalt;
        saveText = Txt(fr, "", 12f, new Vector2(128, 32), new Vector2(460, 18), Cobalt, TextAlignmentOptions.BottomLeft); saveText.characterSpacing = 6f;

        // 码数牌：右上角 260×180（AE (1540,120)），贴右缘
        yard = new GameObject("Yards", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(RectMask2D)).GetComponent<RectTransform>();
        yard.SetParent(root, false); yard.anchorMin = yard.anchorMax = yard.pivot = new Vector2(1f, 1f);
        yard.anchoredPosition = new Vector2(-120f, -120f); yard.sizeDelta = new Vector2(260, 180);
        var yi = yard.GetComponent<Image>(); yi.color = Cobalt; yi.raycastTarget = false;
        yardGroup = yard.GetComponent<CanvasGroup>(); yardGroup.alpha = 0f; yardGroup.blocksRaycasts = false;
        yardLab = Txt(yard, "码 · Yards", 12f, Vector2.zero, new Vector2(230, 18), Paper, TextAlignmentOptions.TopLeft);
        yardLab.rectTransform.anchorMin = yardLab.rectTransform.anchorMax = yardLab.rectTransform.pivot = new Vector2(0f, 1f);
        yardLab.rectTransform.anchoredPosition = new Vector2(14, -12); yardLab.characterSpacing = 6f;
        yardNum = Txt(yard, "9", 170f, Vector2.zero, new Vector2(250, 200), Paper, TextAlignmentOptions.BottomLeft);
        yardNum.rectTransform.anchorMin = yardNum.rectTransform.anchorMax = yardNum.rectTransform.pivot = Vector2.zero;
        yardNum.rectTransform.anchoredPosition = new Vector2(4, -30); yardNum.fontStyle = FontStyles.Bold; yardNum.characterSpacing = -5f;
    }

    void FindRows()
    {
        var L = dm.dialogueLines; if (L == null) return;
        for (int i = 0; i < L.Count; i++)
            if (L[i].text != null && L[i].text.Contains(FoldKey)) { foldRow = i; break; }
        if (foldRow < 0) return;
        int start = foldRow, no = 0;
        while (start > 0 && !(L[start].flow != null && L[start].flow.StartsWith("label:"))) start--;
        for (int i = start; i <= foldRow; i++) if (!string.IsNullOrEmpty(L[i].text)) no++;
        foldNo = no;
        foldEnd = L.Count;
        for (int i = foldRow + 1; i < L.Count; i++) if (L[i].flow != null && L[i].flow.StartsWith("label:")) { foldEnd = i; break; }
    }

    void Update()
    {
        if (dm == null) return;
        if (foldRow < 0 && dm.dialogueLines != null && dm.dialogueLines.Count > 0) FindRows();
        int idx = dm.CurrentIndex;
        if (idx != lastIndex)
        {
            if (foldRow >= 0)
            {
                bool inside = idx >= foldRow && idx < foldEnd;
                if (inside) { if (foldState != 1) { foldState = 1; foldT0 = idx == foldRow && lastIndex < foldRow ? Time.time : Time.time - 5f; } }   // 正好读到：折；读档落在后面：已经折好
                else if (foldState == 1) foldState = 2;                                                                            // 出了这一章：淡掉
            }
            OnLine(idx);
            lastIndex = idx;
        }
        UpdateFold();
        UpdateYard();
    }

    void UpdateFold()
    {
        if (foldState == 0) { foldGroup.alpha = 0f; return; }
        if (foldState == 2) { foldGroup.alpha = Mathf.MoveTowards(foldGroup.alpha, 0f, Time.deltaTime / 0.5f); if (foldGroup.alpha <= 0f) foldState = 0; return; }
        foldGroup.alpha = 1f;
        float fp = EOut(Mathf.Clamp01((Time.time - foldT0) / 0.52f));
        hole.color = new Color(hole.color.r, hole.color.g, hole.color.b, fp);
        flap.Set(new Vector2(0, 112), new Vector2(112, 0), new Vector2(fp * 112f, fp * 112f));
        string s = Time.time - foldT0 > 0.42f ? "折角 · 已自动存档 · 1." + foldNo.ToString("00") : "";
        if (saveText.text != s) saveText.text = s;
    }

    void OnLine(int idx)
    {
        curYard = null; yardK = -1; doneT = -1f; lineT0 = Time.time;
        var L = dm.dialogueLines;
        if (L == null || idx < 0 || idx >= L.Count || L[idx].text == null) { yardN = -99; return; }
        foreach (var y in Yards) if (L[idx].text.Contains(y.key)) { curYard = y; break; }
        if (curYard == null) yardN = -99;
        else { yardLab.text = curYard.lab ?? "码 · Yards"; stallT = -9f; }
    }

    void UpdateYard()
    {
        if (curYard == null) { yardGroup.alpha = Mathf.MoveTowards(yardGroup.alpha, 0f, Time.deltaTime / 0.3f); return; }
        string full = DialogueManager.currentLine == null ? "" : DialogueManager.currentLine.Replace("{w}", "");
        float rv = full.Length > 0 && dm.dialogueText != null ? Mathf.Clamp01(dm.dialogueText.text.Length / (float)full.Length) : 0f;
        if (rv >= 1f && doneT < 0f) doneT = Time.time;
        var Y = curYard;
        int k = Mathf.Min(Y.s.Length - 1, Mathf.FloorToInt(rv * Y.s.Length)), yn = Y.s[k];
        if (k != yardK) { if (yardK >= 0 && Y.sv == yn && yn == yardN) { stallT = Time.time; Sfx("Fly/sfx_reg"); } yardK = k; }
        if (yn != yardN)
        {
            if (yardN != -99) Sfx("Fly/sfx_yard" + Mathf.Clamp(yn < yardN ? 9 - yn : yn, 0, 8));
            yardN = yn; yardNum.text = yn.ToString();
        }
        float a = 1f, oy = 0f, rot = 0f;
        float ox = Time.time - stallT < 0.32f ? Mathf.Sin(Time.time * 1000f / 22f) * 5f : 0f;      // 卡住：抖 0.32 秒
        if (Y.outDir != 0)
        {
            float t0 = Y.atStart ? lineT0 : doneT >= 0f ? doneT + 0.2f : float.MaxValue;
            float u = Mathf.Clamp01((Time.time - t0) / (Y.outDir < 0 ? 0.6f : 0.9f));
            if (Y.outDir < 0) { a = 1f - u; oy = u * u * u * 380f; }                                 // 离地：往上飞走
            else { oy = -u * u * u * 760f; rot = -u * 14f; }                                         // 失败：掉下去，歪着
        }
        if (yardGroup.alpha < a) yardGroup.alpha = Mathf.MoveTowards(yardGroup.alpha, a, Time.deltaTime / 0.25f);
        else yardGroup.alpha = a;
        yard.anchoredPosition = new Vector2(-120f + ox, -120f + oy);
        yard.localEulerAngles = new Vector3(0, 0, rot);
    }

    void Sfx(string n) { if (audioP != null) audioP.PlaySfx(n); }
    static float EOut(float u) { return 1f - Mathf.Pow(1f - u, 3f); }

    Tri MakeTri(RectTransform parent, string n, Color c)
    {
        var rt = new GameObject(n, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero; rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(112, 112);
        var t = rt.gameObject.AddComponent<Tri>(); t.color = c; t.raycastTarget = false; return t;
    }

    TextMeshProUGUI Txt(RectTransform parent, string s, float size, Vector2 pos, Vector2 box, Color c, TextAlignmentOptions al)
    {
        var rt = new GameObject("T", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero; rt.anchoredPosition = pos; rt.sizeDelta = box;
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>(); if (font != null) t.font = font;
        t.text = s; t.fontSize = size; t.color = c; t.alignment = al; t.enableWordWrapping = false; t.raycastTarget = false;
        return t;
    }
}

// 一个三角形（可带描边）：折角的「洞」和「翻起来的那片纸」。坐标以左下角为原点。
public class Tri : MaskableGraphic
{
    Vector2 a, b, c;
    public float stroke; public Color strokeColor = Color.black;

    public void Set(Vector2 p0, Vector2 p1, Vector2 p2) { a = p0; b = p1; c = p2; SetVerticesDirty(); }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if ((b - a).sqrMagnitude < 0.01f && (c - a).sqrMagnitude < 0.01f) return;
        Color32 col = color;
        vh.AddVert(a, col, Vector2.zero); vh.AddVert(b, col, Vector2.zero); vh.AddVert(c, col, Vector2.zero);
        vh.AddTriangle(0, 1, 2);
        if (stroke <= 0f) return;
        Color32 sc = new Color(strokeColor.r, strokeColor.g, strokeColor.b, strokeColor.a * color.a);
        Edge(vh, a, b, sc); Edge(vh, b, c, sc); Edge(vh, c, a, sc);
    }

    void Edge(VertexHelper vh, Vector2 p, Vector2 q, Color32 col)
    {
        Vector2 d = q - p; if (d.sqrMagnitude < 0.01f) return;
        Vector2 n = new Vector2(-d.y, d.x).normalized * (stroke * 0.5f);
        int i = vh.currentVertCount;
        vh.AddVert(p - n, col, Vector2.zero); vh.AddVert(p + n, col, Vector2.zero);
        vh.AddVert(q + n, col, Vector2.zero); vh.AddVert(q - n, col, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
    }
}
