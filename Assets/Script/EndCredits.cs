using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

// 片尾名单（分镜第 9 幕 9c）：结局视频 10–34 秒那几页，改成 Unity 实时排版，叠在视频上面。
// 内容读 Resources/EndCredits.txt（作者可以随时改字、加减页数）；时间跟着视频走（长按 4× 快进也同步）。
// 每页 4 秒，点一下翻到下一页（v1.2：名单有自己的时钟——视频放到第 10 秒先暂停，名单放完再从后面接着放）：图从左边擦出来、四角对位标画一圈、横线拉出来、名字一个字一个字打出来、
// 校样行一行行浮上来、巨大的空心页码、蜡笔小涂鸦、下面一排缩略图；最后 12 栏纸条从上往下把整页「划掉」。
// 版式坐标照分镜：1920×1080、左上原点。
public class EndCredits : MonoBehaviour
{
    const float Start = 10f, PageTime = 4f, ResumeAt = 33.2f, Hide = 34.25f;   // 视频第 10 秒暂停、放名单；放完从 33.2 秒接着放（webm 定位不准，提前一点），34.25 秒以后视频自己接手

    static readonly Color Cobalt = new Color(0f, 0.278f, 0.671f);
    static readonly Color Paper = new Color(0.949f, 0.925f, 0.875f);

    class Page { public string role, name, note, bg, caption; public List<string[]> rows = new List<string[]>(); }

    public VideoPlayer video;
    public AudioPlayer audioPlayer;
    public TMP_FontAsset font, nameFont, numFont;               // 小字 / 名字 / 空心页码

    List<Page> pages;
    CanvasGroup group; RectTransform stage;
    int shown = -1;
    bool built;

    // 一页里会动的零件
    RectTransform picMask, picHolder; Image picBase, picLine;
    Image[] regRing = new Image[4]; RectTransform[] regH = new RectTransform[4], regV = new RectTransform[4];
    TextMeshProUGUI caption, role, nameT, note, pageNo, pageOf, footNo, bigNo;
    RectTransform rule;
    readonly List<TextMeshProUGUI> rowK = new List<TextMeshProUGUI>(), rowV = new List<TextMeshProUGUI>();
    readonly List<Image> rowDots = new List<Image>();
    Image doodle; BoilSprite doodleBoil;
    readonly List<Image> thumbs = new List<Image>(); readonly List<RectTransform> thumbFrames = new List<RectTransform>();
    RectTransform currentFrame;
    readonly List<RectTransform> strike = new List<RectTransform>();

    public bool Running { get; set; }
    public bool Busy => state == 1 || state == 2;     // 名单在放 / 刚放完等视频接上：EndingPlayer 的「播完」计时要暂停
    int state;                                         // 0 = 等视频到 10 秒　1 = 放名单　2 = 视频接着放、等它过 34.25 秒　3 = 结束
    float ct, resumeT;                                 // 名单自己的时钟；接上视频以后等了多久

    void Awake()
    {
        var r = (RectTransform)transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        group = gameObject.AddComponent<CanvasGroup>(); group.alpha = 0f; group.blocksRaycasts = false;
    }

    void Build()
    {
        built = true;
        pages = Load();
        var root = (RectTransform)transform;
        gameObject.AddComponent<Bleed>();
        var bg = gameObject.AddComponent<Image>(); bg.color = Paper; bg.raycastTarget = false;   // 不透明：盖住下面视频里画死的那几页
        stage = R("Stage", root, Vector2.zero, new Vector2(1920, 1080));
        stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f); stage.anchoredPosition = Vector2.zero;

        // 四个角的套准十字（整段都在）
        foreach (var p in new[] { new Vector2(60, 40), new Vector2(1860, 40), new Vector2(60, 1040), new Vector2(1860, 1040) })
        {
            var ring = Img(stage, p - new Vector2(10, 10), new Vector2(20, 20), Cobalt); ring.sprite = RingSprite();
            Img(stage, p - new Vector2(20, 0.6f), new Vector2(40, 1.25f), Cobalt);
            Img(stage, p - new Vector2(0.6f, 20), new Vector2(1.25f, 40), Cobalt);
        }
        T(stage, "N°09 · 片尾 · Credits", font, 12f, 120, 48, 600, Cobalt, TextAlignmentOptions.TopLeft, 6f);
        pageOf = T(stage, "", font, 12f, 1800, 48, 300, Cobalt, TextAlignmentOptions.TopRight, 6f);
        footNo = T(stage, "", font, 12f, 1800, 1048, 300, Cobalt, TextAlignmentOptions.TopRight, 6f);
        T(stage, Loc.F("片尾 · {0} 页", pages.Count.ToString("00")), font, 12f, 120, 920, 400, Cobalt, TextAlignmentOptions.TopLeft, 6f);

        // 左：图（从左往右擦出来，同时慢慢推远）
        picMask = R("Pic", stage, new Vector2(120, 200), new Vector2(828, 466));
        picMask.gameObject.AddComponent<RectMask2D>();
        picHolder = R("Holder", picMask, Vector2.zero, new Vector2(828, 466));
        picHolder.pivot = new Vector2(0.5f, 0.5f); picHolder.anchoredPosition = new Vector2(414, -233);
        picBase = Img(picHolder, Vector2.zero, new Vector2(828, 466), Color.white);
        picLine = Img(picHolder, Vector2.zero, new Vector2(828, 466), Color.white);
        picLine.gameObject.AddComponent<BoilSprite>();
        var marks = new[] { new Vector2(96, 176), new Vector2(972, 176), new Vector2(96, 690), new Vector2(972, 690) };
        for (int q = 0; q < 4; q++)
        {
            regRing[q] = Img(stage, marks[q] - new Vector2(8, 8), new Vector2(16, 16), Cobalt);
            regRing[q].sprite = RingSprite(); regRing[q].type = Image.Type.Filled; regRing[q].fillMethod = Image.FillMethod.Radial360; regRing[q].fillOrigin = (int)Image.Origin360.Top;
            regH[q] = Img(stage, marks[q] - new Vector2(16, 0.6f), new Vector2(32, 1.25f), Cobalt).rectTransform;
            regV[q] = Img(stage, marks[q] - new Vector2(0.6f, 16), new Vector2(1.25f, 32), Cobalt).rectTransform;
            foreach (var x in new[] { regH[q], regV[q] }) { var c = x.anchoredPosition + new Vector2(x.sizeDelta.x / 2, -x.sizeDelta.y / 2); x.pivot = new Vector2(0.5f, 0.5f); x.anchoredPosition = c; }
        }
        caption = T(stage, "", font, 13f, 120, 720, 828, Cobalt, TextAlignmentOptions.TopLeft, 0f);

        // 右：横线、角色、名字、说明、校样
        rule = Img(stage, new Vector2(1114, 340), new Vector2(686, 1), Cobalt).rectTransform;
        role = T(stage, "", font, 13f, 1114, 374, 686, Cobalt, TextAlignmentOptions.TopLeft, 0f);
        nameT = T(stage, "", nameFont != null ? nameFont : font, 40f, 1114, 440, 686, Cobalt, TextAlignmentOptions.TopLeft, 0f);
        note = T(stage, "", font, 16f, 1114, 484, 686, Cobalt, TextAlignmentOptions.TopLeft, 0f);
        bigNo = T(stage, "", numFont != null && numFont.HasCharacters("0123456789") ? numFont : font, 360f,   // 页码字库里缺数字就用主字体
             1810, 1000, 900, Cobalt, TextAlignmentOptions.TopRight, 0f);
        bigNo.faceColor = new Color32(0, 0, 0, 0); bigNo.outlineWidth = 0.03f; bigNo.outlineColor = new Color32(0, 71, 171, 255);
        bigNo.transform.SetSiblingIndex(1);                                   // 压在最下面（只在纸上面）
        doodle = Img(stage, new Vector2(1110, 660), new Vector2(360, 150), Color.white);
        doodleBoil = doodle.gameObject.AddComponent<BoilSprite>();

        // 下：所有页的缩略图
        for (int q = 0; q < pages.Count; q++)
        {
            float tx = 120 + q * 138;
            var th = Img(stage, new Vector2(tx, 820), new Vector2(124, 70), Color.white);
            th.sprite = Resources.Load<Sprite>("Sprites/Fly/BG/" + pages[q].bg + "_base");
            th.preserveAspect = false;
            thumbs.Add(th);
            thumbFrames.Add(Frame(stage, new Vector2(tx, 820), new Vector2(124, 70), 1f, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.35f)));
        }
        currentFrame = Frame(stage, new Vector2(117, 817), new Vector2(130, 76), 3f, Cobalt);

        // 最上面：12 栏纸条（每页最后从上往下划掉）
        for (int i = 1; i <= 12; i++)
        {
            float l = i == 1 ? -400 : Col(i) - 12, rr = i == 12 ? 2320 : ColR(i) + 12;   // 两头伸出去，宽屏两边也盖住
            var s = Img(stage, new Vector2(l, 0), new Vector2(rr - l, 1080), Paper).rectTransform;
            strike.Add(s);
        }
    }

    void Update()
    {
        if (!Running || video == null || video.clip == null) { group.alpha = 0f; state = 0; return; }
        float vt = (float)video.time;
        if (state == 0)
        {
            if (vt < Start) { group.alpha = 0f; return; }
            video.Pause(); state = 1; ct = 0f;                               // 视频停在第 10 秒，名单接手
            if (!built) Build();
        }
        if (state == 3) { group.alpha = 0f; return; }
        group.alpha = 1f;

        int n = pages.Count;
        float end = n * PageTime + 0.8f;                                     // 最后一页划掉要 0.8 秒
        if (state == 1)
        {
            ct += Time.deltaTime;
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                int kk = Mathf.Min(n - 1, Mathf.FloorToInt(ct / PageTime));
                float p0 = ct - kk * PageTime;
                ct = p0 < 3.45f ? kk * PageTime + 3.45f : (kk + 1) * PageTime;   // 点一下：这一页直接划掉；正在划就直接下一页
            }
            if (ct >= end)
            {
                state = 2; resumeT = 0f;                                     // 名单放完：整页是纸色，视频从后面接上（被纸盖着，看不见跳转）
                video.time = ResumeAt; video.Play();
            }
        }
        if (state == 2)
        {
            resumeT += Time.deltaTime;
            if (vt >= Hide || resumeT > 4f) { state = 3; group.alpha = 0f; return; }   // 视频到了手写那段（保险：最多等 4 秒）
            ct = end;
        }

        int k = Mathf.Min(n - 1, Mathf.FloorToInt(ct / PageTime));
        float pt = ct - k * PageTime;
        if (k != shown) { shown = k; Fill(k); if (audioPlayer != null) audioPlayer.PlaySfx("Fly/sfx_page"); }

        // 图：从左边擦出来（指数缓出 0.75 秒），然后慢慢推远
        float rv = 1f - Mathf.Pow(2f, -10f * Clamp(pt / 0.75f));
        picMask.sizeDelta = new Vector2(828f * rv, 466f);
        picHolder.localScale = Vector3.one * (1.045f - 0.045f * Clamp(pt / 4f));
        for (int q = 0; q < 4; q++)
        {
            float u = EOut(Clamp((pt - 0.1f - q * 0.08f) / 0.4f));
            regRing[q].fillAmount = u;
            regH[q].localScale = regV[q].localScale = new Vector3(u, u, 1f);
        }
        Up(caption, 720, 0.4f, 0.8f, pt);
        rule.sizeDelta = new Vector2(686f * EOut(Clamp((pt - 0.2f) / 0.5f)), 1f);
        Up(role, 374, 0.35f, 0.7f, pt);
        nameT.maxVisibleCharacters = Mathf.FloorToInt(Clamp((pt - 0.45f) / (Mathf.Max(1, nameT.text.Length) * 0.045f)) * nameT.text.Length);
        Up(note, 484, 0.8f, 1.2f, pt);
        for (int i = 0; i < rowK.Count; i++)
        {
            float u = EOut(Clamp((pt - 0.9f - i * 0.1f) / 0.4f));
            rowK[i].alpha = rowV[i].alpha = u; rowDots[i].color = new Color(Cobalt.r, Cobalt.g, Cobalt.b, u);
        }
        {
            float u = EOut(Clamp((pt - 0.3f) / 0.6f));
            bigNo.alpha = 0.45f * u; Place(bigNo.rectTransform, 1810, 1000 + (1 - u) * 20, bigNo.fontSize, true);
        }
        doodle.color = new Color(1, 1, 1, Clamp((pt - 0.6f) / 0.5f));
        for (int q = 0; q < thumbs.Count; q++)
        {
            float u = EOut(Clamp((pt - 0.2f - q * 0.05f) / 0.4f));
            thumbs[q].color = new Color(1, 1, 1, (q == k ? 1f : 0.38f) * u);
            thumbFrames[q].gameObject.SetActive(q != k);
        }
        currentFrame.anchoredPosition = new Vector2(117 + k * 138, -817);
        // 划掉：12 栏错开 0.03 秒，各 0.42 秒
        for (int i = 0; i < 12; i++)
        {
            float p = pt > 3.45f ? EIO(Clamp((pt - 3.45f - i * 0.03f) / 0.42f)) : 0f;
            strike[i].sizeDelta = new Vector2(strike[i].sizeDelta.x, 1080f * p);
        }
    }

    // 换页：把这一页的字和图填进去
    void Fill(int k)
    {
        Page p = pages[k];
        var sb = Resources.Load<Sprite>("Sprites/Fly/BG/" + p.bg + "_base");
        picBase.sprite = sb; picBase.enabled = sb != null;
        var lines = new Sprite[3];
        for (int v = 0; v < 3; v++) lines[v] = Resources.Load<Sprite>("Sprites/Fly/BG/" + p.bg + "_line" + v);
        picLine.GetComponent<BoilSprite>().SetFrames(lines[0] != null ? lines : new Sprite[0]);
        picLine.enabled = lines[0] != null;
        caption.text = Loc.F("图 {0} · {1}", (k + 1).ToString("00"), p.caption);
        role.text = p.role; nameT.text = p.name; note.text = p.note;
        pageOf.text = (k + 1).ToString("00") + " / " + pages.Count.ToString("00");
        footNo.text = (52 + k).ToString("000");
        bigNo.text = (k + 1).ToString("00");

        foreach (var t in rowK) Destroy(t.gameObject);
        foreach (var t in rowV) Destroy(t.gameObject);
        foreach (var t in rowDots) Destroy(t.gameObject);
        rowK.Clear(); rowV.Clear(); rowDots.Clear();
        for (int i = 0; i < p.rows.Count; i++)
        {
            float y = 540 + i * 30;
            var a = T(stage, p.rows[i][0], font, 14f, 1114, y, 340, Cobalt, TextAlignmentOptions.TopLeft, 0f);
            var b = T(stage, p.rows[i][1], font, 14f, 1800, y, 560, Cobalt, TextAlignmentOptions.TopRight, 0f);
            float l0 = 1114 + a.GetPreferredValues(a.text).x + 12, l1 = 1800 - b.GetPreferredValues(b.text).x - 12;
            var dots = Img(stage, new Vector2(l0, y - 4), new Vector2(Mathf.Max(0, l1 - l0), 1), Cobalt);   // 点线引导：每 4 像素一个点
            dots.sprite = DotSprite(); dots.type = Image.Type.Tiled;
            rowK.Add(a); rowV.Add(b); rowDots.Add(dots);
        }

        foreach (var st in strike) st.SetAsLastSibling();                    // 校样行是每页新建的，会排到纸条上面去 → 把 12 条纸条重新放回最上层

        var frames = new Sprite[3];
        for (int v = 0; v < 3; v++) frames[v] = Resources.Load<Sprite>("Sprites/Fly/End/doodle" + k + "_v" + v);
        doodle.enabled = frames[0] != null;
        doodleBoil.SetFrames(frames[0] != null ? frames : new Sprite[0]);
        if (frames[0] != null) doodle.sprite = frames[0];
    }

    // 读 EndCredits.txt：一段一页
    static List<Page> Load()
    {
        var list = new List<Page>();
        var ta = Resources.Load<TextAsset>(Loc.Res("EndCredits"));
        if (ta == null) { Debug.LogWarning("找不到 Resources/EndCredits.txt"); return list; }
        var buf = new List<string>();
        foreach (string raw in (ta.text.Replace("\r", "") + "\n\n").Split('\n'))
        {
            string l = raw.Trim();
            if (l.StartsWith("#")) continue;
            if (l.Length > 0) { buf.Add(l); continue; }
            if (buf.Count >= 4)
            {
                var p = new Page { role = buf[0], name = buf[1], note = buf[2] };
                var pic = buf[3].Split('|');
                p.bg = pic[0].Trim(); p.caption = pic.Length > 1 ? pic[1].Trim() : "";
                for (int i = 4; i < buf.Count; i++) { var kv = buf[i].Split('|'); p.rows.Add(new[] { kv[0].Trim(), kv.Length > 1 ? kv[1].Trim() : "" }); }
                list.Add(p);
            }
            else if (buf.Count > 0) Debug.LogWarning("EndCredits.txt 有一段不到 4 行，跳过：" + buf[0]);
            buf.Clear();
        }
        return list;
    }

    // ───────── 小工具 ─────────
    static float Clamp(float x) => Mathf.Clamp01(x);
    static float EOut(float x) => 1f - Mathf.Pow(1f - x, 3f);
    static float EIO(float x) => x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
    static float Col(int i) => 120 + (i - 1) * 142;
    static float ColR(int i) => Col(i) + 118;

    // 淡入并往上浮一条基线（分镜的 up()）
    static void Up(TextMeshProUGUI t, float baseline, float a, float b, float pt)
    {
        float u = EOut(Clamp((pt - a) / (b - a)));
        t.alpha = u;
        Place(t.rectTransform, t.rectTransform.anchoredPosition.x + (t.alignment == TextAlignmentOptions.TopRight ? t.rectTransform.sizeDelta.x : 0), baseline + (1 - u) * 10, t.fontSize, t.alignment == TextAlignmentOptions.TopRight);
    }

    // 按「基线」摆字：canvas 的 fillText 是基线对齐，TMP 是框的上沿；框上沿 ≈ 基线 − 字号
    static void Place(RectTransform rt, float x, float baseline, float size, bool right)
    {
        rt.anchoredPosition = new Vector2(right ? x - rt.sizeDelta.x : x, -(baseline - size * 0.98f));
    }

    static RectTransform R(string n, RectTransform parent, Vector2 pos, Vector2 size)
    {
        var rt = new GameObject(n, typeof(RectTransform)).GetComponent<RectTransform>(); rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(pos.x, -pos.y); rt.sizeDelta = size;
        return rt;
    }

    static Image Img(RectTransform parent, Vector2 pos, Vector2 size, Color c)
    {
        var im = R("Img", parent, pos, size).gameObject.AddComponent<Image>(); im.color = c; im.raycastTarget = false; return im;
    }

    static RectTransform Frame(RectTransform parent, Vector2 pos, Vector2 size, float w, Color c)
    {
        var f = R("Frame", parent, pos, size);
        Img(f, Vector2.zero, new Vector2(size.x, w), c); Img(f, new Vector2(0, size.y - w), new Vector2(size.x, w), c);
        Img(f, Vector2.zero, new Vector2(w, size.y), c); Img(f, new Vector2(size.x - w, 0), new Vector2(w, size.y), c);
        return f;
    }

    TextMeshProUGUI T(RectTransform parent, string s, TMP_FontAsset f, float size, float x, float baseline, float w, Color c, TextAlignmentOptions al, float spacing)
    {
        var t = R("T", parent, Vector2.zero, new Vector2(w, size * 1.5f)).gameObject.AddComponent<TextMeshProUGUI>();
        if (f != null) t.font = f;
        t.text = Loc.T(s); t.fontSize = size; t.color = c; t.alignment = al; t.characterSpacing = spacing;
        t.enableWordWrapping = false; t.raycastTarget = false;
        Place(t.rectTransform, x, baseline, size, al == TextAlignmentOptions.TopRight);
        return t;
    }

    static Sprite ring, dot;
    static Sprite RingSprite()
    {
        if (ring != null) return ring;
        int n = 64; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false); float c = (n - 1) * 0.5f, r = n * 0.5f - 2f;
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float d = Mathf.Abs(Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) - r); tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(2.2f - d))); }
        tex.Apply(); return ring = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
    static Sprite DotSprite()
    {
        if (dot != null) return dot;
        var tex = new Texture2D(4, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        tex.SetPixels(new[] { Color.white, Color.clear, Color.clear, Color.clear }); tex.Apply();
        return dot = Sprite.Create(tex, new Rect(0, 0, 4, 1), new Vector2(0.5f, 0.5f), 100f);
    }
}
