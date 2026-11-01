using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 选语言（v1.3）：第一次打开游戏时，在创作声明之前。版式跟声明页一样：纸色、12 栏网格、套准十字、左栏大标题 + 粗钴蓝条，
// 右栏三个大选项（中文 / English / 日本語），鼠标移上去钴蓝色块从左边刷过去，点一下就选定。键盘：↑↓ 选、回车确认。
// 选完记在 PlayerPrefs（Loc.Lang），以后不再问；设置页里可以改。
public class LangSelect : MonoBehaviour
{
    static readonly Color Cobalt = new Color(0f, 0.278f, 0.671f);
    static readonly Color Paper = new Color(0.949f, 0.925f, 0.875f);

    static readonly string[] Labels = { "中文", "English", "日本語" };
    static readonly string[] Subs = { "简体中文 · CHINESE (SIMPLIFIED)", "ENGLISH", "日本語 · JAPANESE" };

    TMP_FontAsset uiFont, textFont;
    System.Action done;
    RectTransform stage; CanvasGroup group; AudioSource se;
    readonly List<RectTransform> fills = new List<RectTransform>();
    readonly List<TextMeshProUGUI[]> rowTexts = new List<TextMeshProUGUI[]>();
    readonly List<RectTransform> rows = new List<RectTransform>();
    readonly List<CanvasGroup> fadeIns = new List<CanvasGroup>();
    readonly List<float> delays = new List<float>();
    readonly float[] fillP = new float[3];             // 每一行色块刷到哪了（0–1）
    int sel; float t; bool leaving;
    RectTransform glider; CanvasGroup gliderG; Image[] trail;   // 左栏空白处：一架蜡笔滑翔机沿着虚线一遍遍滑过去
    static readonly Vector2 A0 = new Vector2(150, 900), A1 = new Vector2(420, 610), A2 = new Vector2(790, 770);
    const float Lap = 5.5f, FlyTime = 4f;

    public static void Show(Transform canvas, TMP_FontAsset ui, TMP_FontAsset text, System.Action onDone)
    {
        var go = new GameObject("LangSelect", typeof(RectTransform));
        go.transform.SetParent(canvas, false); go.transform.SetAsLastSibling();
        var ls = go.AddComponent<LangSelect>();
        ls.uiFont = ui; ls.textFont = text; ls.done = onDone;
        ls.Build();
    }

    void Build()
    {
        var r = (RectTransform)transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        gameObject.AddComponent<Bleed>();
        gameObject.AddComponent<Image>().color = Paper;
        group = gameObject.AddComponent<CanvasGroup>();
        se = gameObject.AddComponent<AudioSource>(); se.playOnAwake = false; se.volume = SaveSystem.Sfx;
        stage = R("Stage", r, Vector2.zero, new Vector2(1920, 1080));
        stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f); stage.anchoredPosition = Vector2.zero;
        sel = System.Array.IndexOf(Loc.Codes, Loc.Guess());

        // 网格、套准十字、裁切线（和声明页一样的「印张」）
        for (int c = 0; c < 12; c++)
        {
            float x0 = 120 + c * 142;
            In(Img(x0, 80, 1, 920, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.10f)), 0.02f * c);
            In(Img(x0 + 118, 80, 1, 920, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.10f)), 0.02f * c + 0.01f);
        }
        In(Img(0, 80, 1920, 1, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.22f)), 0f);
        In(Img(0, 1000, 1920, 1, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.22f)), 0.1f);
        foreach (var p in new[] { new Vector2(56, 56), new Vector2(1864, 56), new Vector2(56, 1024), new Vector2(1864, 1024) })
        {
            var ring = Img(p.x - 9, p.y - 9, 18, 18, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.7f)); ring.sprite = RingSprite(); In(ring, 0.1f);
            In(Img(p.x - 16, p.y, 32, 1, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.7f)), 0.1f);
            In(Img(p.x, p.y - 16, 1, 32, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.7f)), 0.1f);
        }
        In(Cap("N°00 · 语言 · LANGUAGE · 言語", 120, 32, 700), 0.2f);
        In(Cap("飞 / FLY — 选择语言 · LANGUAGE", 1200, 32, 600, TextAlignmentOptions.TopRight), 0.25f);
        In(Cap("一部视觉小说 · A VISUAL NOVEL · ビジュアルノベル", 120, 1032, 700), 0.3f);
        In(Cap("P. 000", 1500, 1032, 300, TextAlignmentOptions.TopRight), 0.35f);

        // 左栏：三种语言的标题叠在一起 + 粗钴蓝条 + 说明
        In(Cap("N°00 — 语言 · LANGUAGE", 120, 112, 686), 0.3f);
        In(Txt("选择语言", uiFont, 46f, 120, 136, 686, 60, FontStyles.Bold), 0.4f);
        In(Txt("Choose your language", uiFont, 30f, 120, 200, 686, 40, FontStyles.Bold), 0.48f);
        In(Txt("言語を選んでください", uiFont, 26f, 120, 244, 686, 36, FontStyles.Bold), 0.56f);
        In(Img(120, 300, 686, 5, Cobalt), 0.65f);
        var note = Txt("之后可以在「设置」里更改。\nYou can change this later in Settings.\n後から「設定」で変更できます。", textFont != null ? textFont : uiFont, 18f, 120, 324, 686, 120);
        note.enableWordWrapping = true; note.lineSpacing = 18f; In(note, 0.75f);
        In(Cap("点一下选择 · CLICK TO CHOOSE · タップして選択", 120, 944, 686), 0.9f);

        // 空心的「文 A あ」：只有钴蓝描边，像套印前的样张
        var glyphs = Txt("文 A あ", uiFont, 150f, 112, 452, 700, 190, FontStyles.Bold);
        glyphs.fontMaterial = new Material(glyphs.fontSharedMaterial);
        glyphs.faceColor = new Color32(0, 0, 0, 0); glyphs.outlineWidth = 0.14f; glyphs.outlineColor = new Color32(0, 71, 171, 150);
        glyphs.characterSpacing = 6f; In(glyphs, 0.85f);
        // 蜡笔滑翔机（声明页那三帧）+ 身后一串点：和下一页「等一阵风」接得上
        trail = new Image[34];
        var dot = DotSprite();
        for (int i = 0; i < trail.Length; i++) { trail[i] = Img(0, 0, 5, 5, Cobalt); trail[i].sprite = dot; trail[i].enabled = false; }
        var frames = new Sprite[3]; for (int v = 0; v < 3; v++) frames[v] = Resources.Load<Sprite>("Sprites/Fly/UI/ai_glider_v" + v);
        if (frames[0] != null)
        {
            var g = Img(0, 0, 196, 77, Color.white); g.preserveAspect = true;   // 比声明页里那架大一号：这里它是装饰，要看得清
            glider = g.rectTransform; glider.pivot = new Vector2(0.5f, 0.5f);
            g.gameObject.AddComponent<BoilSprite>().SetFrames(frames);
            gliderG = g.gameObject.AddComponent<CanvasGroup>(); gliderG.alpha = 0f;
        }

        // 右栏（第 7–12 栏，972–1800）：三个大选项
        for (int i = 0; i < 3; i++)
        {
            int k = i; float y = 200 + i * 220;
            var row = R("Row" + i, stage, new Vector2(972, y), new Vector2(828, 196));
            row.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);          // 接住点击
            var fill = R("Fill", row, Vector2.zero, new Vector2(828, 196));
            fill.gameObject.AddComponent<Image>().color = Cobalt; fill.GetComponent<Image>().raycastTarget = false;
            fill.localScale = new Vector3(0f, 1f, 1f);
            Child(row, Img(0, 0, 828, 1, Cobalt));
            var num = Txt("0" + (i + 1), uiFont, 13f, 24, 22, 60, 18); num.characterSpacing = 8f; Child(row, num);
            var big = Txt(Labels[i], uiFont, 88f, 24, 40, 700, 110, FontStyles.Bold); Child(row, big);
            var sub = Txt(Subs[i], uiFont, 13f, 24, 160, 700, 18); sub.characterSpacing = 8f; Child(row, sub);
            var arrow = Txt("→", uiFont, 44f, 700, 64, 104, 60, FontStyles.Normal, TextAlignmentOptions.TopRight); Child(row, arrow);
            var btn = row.gameObject.AddComponent<Button>(); btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => Choose(k));
            var hover = row.gameObject.AddComponent<HoverHook>(); hover.onEnter = () => { if (sel != k && !leaving) { sel = k; Play("sfx_tick"); } };
            fills.Add(fill); rows.Add(row); rowTexts.Add(new[] { num, big, sub, arrow });
            var g = row.gameObject.AddComponent<CanvasGroup>(); g.alpha = 0f; fadeIns.Add(g); delays.Add(0.8f + i * 0.1f);
        }
        Play("sfx_page");
    }

    // 开场视频那一层（透明，但接点击）是在我们之后才生成的，会盖在上面把鼠标点击吃掉：每一帧都把自己提到最上层
    void LateUpdate() { if (!leaving && transform.GetSiblingIndex() != transform.parent.childCount - 1) transform.SetAsLastSibling(); }

    void Update()
    {
        t += Time.unscaledDeltaTime;
        if (!leaving)
        {
            for (int i = 0; i < fadeIns.Count; i++)
            {
                float u = Mathf.Clamp01((t - delays[i]) / 0.6f), e = 1f - Mathf.Pow(1f - u, 3f);
                fadeIns[i].alpha = e;
            }
            if (Input.GetKeyDown(KeyCode.UpArrow) && sel > 0) { sel--; Play("sfx_tick"); }
            if (Input.GetKeyDown(KeyCode.DownArrow) && sel < 2) { sel++; Play("sfx_tick"); }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) Choose(sel);
        }
        Glide();
        // 选中的那一行：钴蓝色块从左边刷过去，字变纸色
        for (int i = 0; i < fills.Count; i++)
        {
            fillP[i] = Mathf.MoveTowards(fillP[i], i == sel ? 1f : 0f, Time.unscaledDeltaTime / 0.22f);
            float x = 1f - Mathf.Pow(1f - fillP[i], 3f);                     // 缓出：刷得快、停得轻
            fills[i].localScale = new Vector3(x, 1f, 1f);
            Color c = Color.Lerp(Cobalt, Paper, x);
            foreach (var tx in rowTexts[i]) tx.color = new Color(c.r, c.g, c.b, tx.color.a);
        }
    }

    static Vector2 Bez(float u) => (1 - u) * (1 - u) * A0 + 2 * (1 - u) * u * A1 + u * u * A2;

    // 一圈 5.5 秒：0–4 秒滑过去（缓入缓出），点一个一个落在身后；4–5 秒滑翔机和点一起淡掉；空 0.5 秒再来
    void Glide()
    {
        if (glider == null) return;
        float ct = t - 1.2f; if (ct < 0f) return;
        ct %= Lap;
        float u = Mathf.Clamp01(ct / FlyTime); u = u * u * (3f - 2f * u);
        float fade = 1f - Mathf.Clamp01((ct - FlyTime) / 1f);
        Vector2 p = Bez(u), q = Bez(Mathf.Min(1f, u + 0.01f)) - Bez(Mathf.Max(0f, u - 0.01f));
        glider.anchoredPosition = new Vector2(p.x, -p.y);
        glider.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(-q.y, q.x) * Mathf.Rad2Deg + Mathf.Sin(t * 2.1f) * 3f);   // 跟着弧线抬头、低头，再轻轻晃
        gliderG.alpha = Mathf.Clamp01(ct / 0.3f) * fade * (leaving ? 0f : 1f);
        for (int i = 0; i < trail.Length; i++)
        {
            float ui = (i + 0.5f) / trail.Length;
            bool on = ui < u - 0.04f && !leaving;
            trail[i].enabled = on;
            if (!on) continue;
            Vector2 d = Bez(ui); trail[i].rectTransform.anchoredPosition = new Vector2(d.x, -d.y);
            trail[i].color = new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.55f * fade);
        }
    }

    void Choose(int k)
    {
        if (leaving) return;
        leaving = true; sel = k;
        Loc.Lang = Loc.Codes[k];
        Play("sfx_stamp");
        StartCoroutine(Leave());
    }

    IEnumerator Leave()
    {
        yield return new WaitForSecondsRealtime(0.35f);
        Play("sfx_page");
        for (float u = 0f; u < 0.5f; u += Time.unscaledDeltaTime) { group.alpha = 1f - u / 0.5f; yield return null; }
        done?.Invoke();
        Destroy(gameObject);
    }

    // ───────── 零件（坐标照分镜：1920×1080、左上原点） ─────────
    void In(Graphic g, float delay)
    {
        var cg = g.gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0f; fadeIns.Add(cg); delays.Add(delay);
    }

    RectTransform R(string n, RectTransform parent, Vector2 pos, Vector2 size)
    {
        var rt = new GameObject(n, typeof(RectTransform)).GetComponent<RectTransform>(); rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(pos.x, -pos.y); rt.sizeDelta = size;
        return rt;
    }

    Image Img(float x, float y, float w, float h, Color c)
    {
        var im = R("Img", stage, new Vector2(x, y), new Vector2(w, h)).gameObject.AddComponent<Image>(); im.color = c; im.raycastTarget = false; return im;
    }

    static void Child(RectTransform parent, Graphic g) => g.rectTransform.SetParent(parent, false);

    TextMeshProUGUI Txt(string s, TMP_FontAsset f, float size, float x, float y, float w, float h, FontStyles st = FontStyles.Normal, TextAlignmentOptions al = TextAlignmentOptions.TopLeft)
    {
        var t = R("T", stage, new Vector2(x, y), new Vector2(w, h)).gameObject.AddComponent<TextMeshProUGUI>();
        if (f != null) t.font = f;
        t.text = s; t.fontSize = size; t.color = Cobalt; t.fontStyle = st; t.alignment = al; t.enableWordWrapping = false; t.raycastTarget = false;
        return t;
    }

    TextMeshProUGUI Cap(string s, float x, float y, float w, TextAlignmentOptions al = TextAlignmentOptions.TopLeft)
    {
        var t = Txt(s, uiFont, 12f, x, y, w, 16, FontStyles.Normal, al); t.characterSpacing = 9f; return t;
    }

    void Play(string n) { var c = Resources.Load<AudioClip>("Audio/Fly/" + n); if (c != null) se.PlayOneShot(c); }

    static Sprite dotS;
    static Sprite DotSprite()
    {
        if (dotS != null) return dotS;
        int n = 32; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false); float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float dd = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)); tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(c - dd + 0.5f))); }
        tex.Apply(); return dotS = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite ring;
    static Sprite RingSprite()
    {
        if (ring != null) return ring;
        int n = 64; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false); float c = (n - 1) * 0.5f, r = n * 0.5f - 2f;
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float d = Mathf.Abs(Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) - r); tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(2.2f - d))); }
        tex.Apply(); return ring = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    // 鼠标移上去：换选中（手机上没有悬停，点一下就是选定）
    class HoverHook : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler
    {
        public System.Action onEnter;
        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { onEnter?.Invoke(); }
    }
}
