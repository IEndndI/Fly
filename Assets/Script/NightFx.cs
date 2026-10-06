using UnityEngine;
using UnityEngine.UI;

// 结局 A 回家的夜路：「星星」那句天上的星星慢慢浮出来（蜡笔，3 帧抖动），「飞虫」那句两盏路灯下各有 16 只小虫绕着飞。
// 飞虫的轨迹和分镜一模一样（同一个 hh 哈希算出半径、速度、相位）。表格 effect 列：night:stars / night:bugs / night:off。
// 界面零件在 Awake 里自己生成：挂到 Canvas 下背景线稿上面的一个空物体上。
public class NightFx : MonoBehaviour
{
    static readonly Vector2[] Lamps = { new Vector2(558, 548), new Vector2(1558, 588) };
    CanvasGroup stars; RectTransform[] bugs; Image[] bugImg;
    float starsT = -1f, bugsT = -1f, t;

    void Awake()
    {
        var rt = (RectTransform)transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;

        var s = new GameObject("Stars", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        var sr = s.GetComponent<RectTransform>(); sr.SetParent(rt, false);
        sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = sr.offsetMax = Vector2.zero;
        s.GetComponent<Image>().raycastTarget = false;
        var f = new Sprite[3]; for (int v = 0; v < 3; v++) f[v] = Resources.Load<Sprite>("Sprites/Fly/FX/night_stars_v" + v);
        s.AddComponent<BoilSprite>().SetFrames(f[0] != null ? f : new Sprite[0]);
        stars = s.GetComponent<CanvasGroup>(); stars.alpha = 0f;

        Sprite dot = DotSprite();
        bugs = new RectTransform[32]; bugImg = new Image[32];
        for (int i = 0; i < 32; i++)
        {
            var b = new GameObject("Bug", typeof(RectTransform), typeof(Image));
            var br = b.GetComponent<RectTransform>(); br.SetParent(rt, false);
            br.anchorMin = br.anchorMax = new Vector2(0f, 1f); br.pivot = new Vector2(0.5f, 0.5f);
            Vector2 L = Lamps[i / 16]; int k = i % 16;
            float d = 2f * (1.8f + Hh(k + (int)L.x) * 1.8f);
            br.sizeDelta = new Vector2(d, d);
            bugImg[i] = b.GetComponent<Image>(); bugImg[i].sprite = dot; bugImg[i].raycastTarget = false;
            bugImg[i].color = new Color(0.984f, 0.973f, 0.941f, 0f);
            bugs[i] = br;
        }
    }

    public void Command(string arg)
    {
        if (arg == "stars") starsT = 0f;
        else if (arg == "bugs") bugsT = 0f;
        else if (arg == "off") { starsT = bugsT = -1f; stars.alpha = 0f; foreach (var i in bugImg) i.color = new Color(i.color.r, i.color.g, i.color.b, 0f); }
        else Debug.LogWarning("night: 不认识的参数 " + arg);
    }

    void Update()
    {
        t += Time.deltaTime;
        if (starsT >= 0f) { starsT += Time.deltaTime; stars.alpha = Ease(Mathf.Clamp01((starsT - 0.4f) / 3f)); }
        if (bugsT < 0f) return;
        bugsT += Time.deltaTime;
        float b = Ease(Mathf.Clamp01((bugsT - 0.2f) / 2.2f));
        for (int i = 0; i < 32; i++)
        {
            Vector2 L = Lamps[i / 16]; int k = i % 16;
            float ph = Hh(k * 13 + (int)L.x) * 6.28f, r = 36f + Hh(k * 7 + (int)L.y) * 74f, sp = 0.6f + Hh(k * 3 + 1) * 1.4f;
            float x = L.x + Mathf.Cos(t * sp + ph) * r, y = L.y + Mathf.Sin(t * sp * 1.3f + ph) * r * 0.6f;
            bugs[i].anchoredPosition = new Vector2(x, -y);
            var c = bugImg[i].color; c.a = b * (0.45f + 0.45f * Mathf.Sin(t * 3f + k)); bugImg[i].color = c;
        }
    }

    static float Ease(float x) => x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;

    // 分镜里的 hh：整数 → [0,1) 的固定随机数
    static float Hh(int a)
    {
        uint x = (uint)a;
        x = (x ^ (x >> 16)) * 0x45d9f3bu;
        x = (x ^ (x >> 16)) * 0x45d9f3bu;
        return (float)((x ^ (x >> 16)) / 4294967296.0);
    }

    static Sprite DotSprite()
    {
        int n = 16; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            { float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)); tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(c - d + 0.5f))); }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}
