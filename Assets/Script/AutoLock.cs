using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 自动播放锁（对应分镜第 2 幕起飞）：左边一个小圈转满一圈、锁扣上 → 之后点击被锁住，
// 每句打完字、留够阅读时间就自己翻页；回到坡脚时解锁。
// 表格 effect 列：lock:on 开始（起跑那句），lock:off 结束（回到坡脚那句）。
// 界面零件全部在 Awake 里自己生成：把这个脚本挂到 Canvas 下一个空物体上就能用。
public class AutoLock : MonoBehaviour
{
    public bool IsLocked { get; private set; }

    [Header("时间")]
    public float armTime = 1.5f;        // 圈转满一圈
    public float readBase = 1.0f;       // 每句打完后至少停多久
    public float readPerChar = 0.07f;   // 每个字再多停多久

    [Header("位置（AE 坐标：左上角原点，1920×1080）")]
    public Vector2 aePos = new Vector2(120, 496);
    public TMP_FontAsset font;          // 留空 = 用 TMP 默认字体

    static readonly Color Cobalt = new Color(0f, 0.278f, 0.671f);
    static readonly Color Paper = new Color(0.949f, 0.925f, 0.875f);

    RectTransform root, shackle;
    CanvasGroup group;
    Image ringBack, ring, body;
    TextMeshProUGUI title, sub;
    Coroutine run;
    AudioPlayer sound;
    float slide;                        // 进场时从左边滑进来
    Vector2 home;

    public float ReadTime(string text) => readBase + readPerChar * (text == null ? 0 : text.Length);

    void Awake()
    {
        sound = FindObjectOfType<AudioPlayer>();
        Build();
        group.alpha = 0f;
    }

    public void Set(bool on)
    {
        if (on == IsLocked && run != null) return;
        if (run != null) StopCoroutine(run);
        run = StartCoroutine(on ? Arm() : Release());
    }

    // 不用光晕描边（看起来像没抠干净）：画面暗了整套换成纸色，1 秒渐变（和四角小字一样）
    float darkK;
    void Update()
    {
        bool dark = (FlightVideo.Instance != null && FlightVideo.Instance.Dark) || GameChrome.DarkK > 0.5f;
        darkK = Mathf.MoveTowards(darkK, dark ? 1f : 0f, Time.deltaTime);
        Color c = Color.Lerp(Cobalt, Paper, darkK);
        foreach (var g in new Graphic[] { title, sub, ring, ringBack, shackle.GetComponent<Image>(), body })
            if (g != null) g.color = new Color(c.r, c.g, c.b, g.color.a);
    }

    // 被锁住时点了一下：轻轻晃一下，告诉读者「现在点不了」
    public void Nudge()
    {
        if (group.alpha < 0.5f) return;
        if (sound != null) sound.PlaySfx("Fly/sfx_tick");
        StartCoroutine(Shake());
    }

    IEnumerator Arm()
    {
        IsLocked = true;
        title.text = "即将自动播放"; sub.text = "AUTO-PLAY AHEAD";
        ring.fillAmount = 0f; ring.color = Cobalt; SetShackle(0f);
        float t = 0f; bool clicked = false;
        while (t < armTime)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Clamp01(t / 0.45f);
            root.anchoredPosition = home + new Vector2(-12f * (1f - EaseOut(Mathf.Clamp01(t / 0.6f))), 0f);
            ring.fillAmount = EaseInOut(Mathf.Clamp01(t / armTime));
            if (!clicked && t >= armTime - 0.95f) { clicked = true; if (sound != null) sound.PlaySfx("Fly/sfx_lock"); }  // 这声的「咔」在 0.95 秒处：正好落在圈合上
            yield return null;
        }
        ring.fillAmount = 1f;
        // 圈合上：锁扣落下
        for (t = 0f; t < 0.28f; t += Time.deltaTime) { SetShackle(Mathf.Pow(t / 0.28f, 3f)); yield return null; }
        SetShackle(1f);
        title.text = "自动播放中"; sub.text = "点击已锁定 · CLICKS LOCKED";
        run = null;
    }

    IEnumerator Release()
    {
        IsLocked = false;
        ring.fillAmount = 1f; ring.color = new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.45f); SetShackle(0f);
        title.text = "已解锁"; sub.text = "UNLOCKED";
        if (sound != null) sound.PlaySfx("Fly/sfx_unlock");
        yield return new WaitForSeconds(1.6f);
        for (float t = 0f; t < 0.45f; t += Time.deltaTime) { group.alpha = 1f - t / 0.45f; yield return null; }
        group.alpha = 0f;
        run = null;
    }

    IEnumerator Shake()
    {
        for (float t = 0f; t < 0.36f; t += Time.deltaTime)
        {
            float u = t / 0.36f, x = u < 0.3f ? Mathf.Lerp(0, 6, u / 0.3f) : u < 0.6f ? Mathf.Lerp(6, -4, (u - 0.3f) / 0.3f) : Mathf.Lerp(-4, 0, (u - 0.6f) / 0.4f);
            root.anchoredPosition = home + new Vector2(x, 0f);
            yield return null;
        }
        root.anchoredPosition = home;
    }

    // 0 = 锁扣抬起（开着），1 = 扣下（锁上）
    void SetShackle(float k) { shackle.anchoredPosition = new Vector2(0f, 7.5f + 3.5f * (1f - k)); }

    static float EaseOut(float x) => 1f - Mathf.Pow(1f - x, 3f);
    static float EaseInOut(float x) => x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;

    // ---------- 生成零件：圈（底圈 + 会转满的圈）、锁（锁身 + 锁扣）、两行字 ----------
    void Build()
    {
        root = (RectTransform)transform;
        root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.sizeDelta = new Vector2(360f, 44f);
        home = new Vector2(aePos.x, -aePos.y);
        root.anchoredPosition = home;
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false; group.interactable = false;

        Sprite ringSpr = MakeSprite(RingTex(128, 2.6f / 44f));
        ringBack = MakeImage("RingBack", root, ringSpr, new Vector2(22, -22), new Vector2(44, 44));
        ringBack.color = new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.22f);
        ring = MakeImage("Ring", root, ringSpr, new Vector2(22, -22), new Vector2(44, 44));
        ring.color = Cobalt; ring.type = Image.Type.Filled; ring.fillMethod = Image.FillMethod.Radial360;
        ring.fillOrigin = (int)Image.Origin360.Top; ring.fillClockwise = true; ring.fillAmount = 0f;

        // 锁：锁身 10×7，锁扣 8×8（落下时插进锁身）
        RectTransform icon = new GameObject("Lock", typeof(RectTransform)).GetComponent<RectTransform>();
        icon.SetParent(root, false); icon.anchorMin = icon.anchorMax = new Vector2(0f, 1f); icon.pivot = new Vector2(0.5f, 0.5f);
        icon.anchoredPosition = new Vector2(22, -24); icon.sizeDelta = new Vector2(10, 10);
        Image bodyImg = MakeImage("Body", icon, null, new Vector2(5, -6.5f), new Vector2(10, 7));
        bodyImg.color = Cobalt; body = bodyImg;
        Image sh = MakeImage("Shackle", icon, MakeSprite(ShackleTex(64)), Vector2.zero, new Vector2(8, 8));
        sh.color = Cobalt;
        shackle = sh.rectTransform; shackle.anchorMin = shackle.anchorMax = new Vector2(0.5f, 0.5f); shackle.pivot = new Vector2(0.5f, 0f);
        SetShackle(0f);

        title = MakeText("Title", 15f, new Vector2(56, -2), 20f);
        sub = MakeText("Sub", 11f, new Vector2(56, -23), 15f);
        sub.characterSpacing = 6f; sub.alpha = 0.75f;
    }

    Image MakeImage(string n, RectTransform parent, Sprite s, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.GetComponent<Image>(); img.sprite = s; img.raycastTarget = false;
        return img;
    }

    TextMeshProUGUI MakeText(string n, float size, Vector2 pos, float h)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(root, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(300f, h);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size; t.color = Cobalt; t.raycastTarget = false;
        t.enableWordWrapping = false; t.alignment = TextAlignmentOptions.MidlineLeft;
        return t;
    }

    static Sprite MakeSprite(Texture2D tex) => Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);

    // 圆环：外径 = 贴图边缘，线宽 w（占半径的比例 × 2），边缘抗锯齿
    static Texture2D RingTex(int n, float w)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = n * 0.5f - 1.5f, half = w * n * 0.5f, c = (n - 1) * 0.5f;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Abs(Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) - (r - half));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(half - d + 0.5f) * 255));
            }
        tex.SetPixels32(px); tex.Apply(); return tex;
    }

    // 锁扣：上面半圆 + 两条竖腿（贴图底边 = 插进锁身的地方）
    static Texture2D ShackleTex(int n)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float cx = (n - 1) * 0.5f, cy = n * 0.62f, r = n * 0.32f, half = n * 0.11f;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x - cx, dy = y - cy, d;
                if (dy >= 0) d = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - r);   // 上半圆（y 向上）
                else d = Mathf.Min(Mathf.Sqrt((Mathf.Abs(dx) - r) * (Mathf.Abs(dx) - r)), 1e6f); // 两条腿
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(half - d + 0.5f) * 255));
            }
        tex.SetPixels32(px); tex.Apply(); return tex;
    }
}
