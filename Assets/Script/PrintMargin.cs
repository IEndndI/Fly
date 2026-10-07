using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 标题场景的「印张页边」（v1.1）：屏幕比 16:9 宽（或高）时，1920×1080 的画面像一张印好的页，
// 外面多出来的部分是印张的纸边——纸色底，四个裁切角线，页边中间一个套准十字，
// 左边竖排一行印刷说明，右边一列色标（钴蓝 / 浅钴蓝 / 黄 / 纸 / 墨），和游戏里的瑞士印刷风格一致。
// 放在标题 Canvas 的最底层（开场视频、标题画面都压在它上面）；16:9 时什么都不显示。
public class PrintMargin : MonoBehaviour
{
    static readonly Color Cobalt = new Color(0f, 0.278f, 0.671f);
    static readonly Color Paper = new Color(0.949f, 0.925f, 0.875f);
    static readonly Color Sun = new Color(0.949f, 0.71f, 0.122f);
    static readonly Color Ink = new Color(0.12f, 0.12f, 0.16f);

    public TMP_FontAsset font;
    RectTransform root, marks; Vector2 builtFor = new Vector2(-1f, -1f);

    public static PrintMargin Create(Transform parent, TMP_FontAsset font)
    {
        var go = new GameObject("PrintMargin", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.transform.SetSiblingIndex(0);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;   // 先拉满，Bleed 再伸到屏幕边
        var pm = go.AddComponent<PrintMargin>(); pm.font = font;
        var img = go.GetComponent<Image>(); img.color = Paper; img.raycastTarget = false;
        go.AddComponent<Bleed>();                                               // 纸色铺到屏幕边
        return pm;
    }

    void LateUpdate()
    {
        if (root == null) root = (RectTransform)transform;
        var ex = UIFrame.Extra(root);
        if ((ex - builtFor).sqrMagnitude < 0.25f) return;                       // 比例没变就不重画
        builtFor = ex;
        if (marks != null) Destroy(marks.gameObject);
        marks = new GameObject("Marks", typeof(RectTransform)).GetComponent<RectTransform>();
        marks.SetParent(root, false);
        marks.anchorMin = marks.anchorMax = marks.pivot = new Vector2(0.5f, 0.5f);
        marks.anchoredPosition = Vector2.zero; marks.sizeDelta = new Vector2(UIFrame.W, UIFrame.H);
        if (ex.x > 24f) BuildSides(ex.x);
        else if (ex.y > 24f) BuildTopBottom(ex.y);
    }

    // 以 1920×1080 画面的左上角为原点、y 向下（AE 坐标），画面外是负数或大于 1920 / 1080
    void BuildSides(float m)
    {
        CropMarks(m, 0f);
        float cx = -m * 0.5f, rx = UIFrame.W + m * 0.5f;
        if (m >= 70f) { Reg(cx, 540f); Reg(rx, 540f); }
        if (m >= 50f)
        {
            // 左：竖排印刷说明
            var t = Txt("飞 FLY　·　PROOF 01　·　ENDND × CLAUDE OPUS 5.5　·　2026.10", 11f, new Vector2(cx, 270f), new Vector2(520f, 16f));
            t.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);
            // 右：色标一列
            float y = 760f;
            foreach (var c in new[] { Cobalt, new Color(0.35f, 0.5f, 0.78f), new Color(0.6f, 0.7f, 0.88f), Sun, Paper, Ink })
            {
                var sw = Box(new Vector2(rx - 8f, y), new Vector2(16f, 22f), c);
                if (c == Paper) Frame(sw, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.5f));
                y += 26f;
            }
            Txt("P. 000", 11f, new Vector2(rx, 290f), new Vector2(80f, 16f)).alignment = TextAlignmentOptions.Center;
        }
    }

    void BuildTopBottom(float m)
    {
        CropMarks(0f, m);
        float cy = -m * 0.5f, by = UIFrame.H + m * 0.5f;
        if (m >= 70f) { Reg(960f, cy); Reg(960f, by); }
        if (m >= 40f)
        {
            Txt("飞 FLY　·　PROOF 01　·　ENDND × CLAUDE OPUS 5.5　·　2026.10", 11f, new Vector2(480f, cy), new Vector2(560f, 16f));
            float x = 1340f;
            foreach (var c in new[] { Cobalt, new Color(0.35f, 0.5f, 0.78f), new Color(0.6f, 0.7f, 0.88f), Sun, Paper, Ink })
            {
                var sw = Box(new Vector2(x, by - 7f), new Vector2(24f, 14f), c);
                if (c == Paper) Frame(sw, new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.5f));
                x += 28f;
            }
        }
    }

    // 裁切角线：每个画面角外侧一个「L」——一条沿着画面边线伸出去（在页边里），一条贴着画面边往里走；2px 粗，缩小也不会时有时无
    void CropMarks(float mx, float my)
    {
        var c = new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.7f);
        float gap = 8f, len = Mathf.Min(28f, Mathf.Max(mx, my) - gap - 4f);
        if (len < 8f) return;
        foreach (var corner in new[] { new Vector2(0f, 0f), new Vector2(UIFrame.W, 0f), new Vector2(0f, UIFrame.H), new Vector2(UIFrame.W, UIFrame.H) })
        {
            float sx = corner.x == 0f ? -1f : 1f, sy = corner.y == 0f ? -1f : 1f;   // 指向画面外的方向
            if (mx > 0f)          // 左右有页边：横线伸进页边，竖短线贴在页边里、往画面内侧走
            {
                Box(new Vector2(corner.x + sx * (gap + len * 0.5f), corner.y - sy * 1f), new Vector2(len, 2f), c);
                Box(new Vector2(corner.x + sx * gap, corner.y - sy * (len * 0.5f)), new Vector2(2f, len), c);
            }
            else                  // 上下有页边
            {
                Box(new Vector2(corner.x - sx * 1f, corner.y + sy * (gap + len * 0.5f)), new Vector2(2f, len), c);
                Box(new Vector2(corner.x - sx * (len * 0.5f), corner.y + sy * gap), new Vector2(len, 2f), c);
            }
        }
    }

    // 套准十字：圆圈 + 十字线
    void Reg(float x, float y)
    {
        var c = new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.7f);
        Box(new Vector2(x, y), new Vector2(36f, 2f), c);
        Box(new Vector2(x, y), new Vector2(2f, 36f), c);
        var ring = Box(new Vector2(x, y), new Vector2(22f, 22f), c);
        ring.sprite = RingSprite();
    }

    Image Box(Vector2 center, Vector2 size, Color c)
    {
        var rt = new GameObject("m", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(marks, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(center.x, -center.y); rt.sizeDelta = size;
        var im = rt.gameObject.AddComponent<Image>(); im.color = c; im.raycastTarget = false; return im;
    }

    void Frame(Image box, Color c)
    {
        var sz = box.rectTransform.sizeDelta; var a = box.rectTransform.anchoredPosition; var ctr = new Vector2(a.x, -a.y);
        Box(ctr + new Vector2(0f, -sz.y * 0.5f + 1f), new Vector2(sz.x, 2f), c);
        Box(ctr + new Vector2(0f, sz.y * 0.5f - 1f), new Vector2(sz.x, 2f), c);
        Box(ctr + new Vector2(-sz.x * 0.5f + 1f, 0f), new Vector2(2f, sz.y), c);
        Box(ctr + new Vector2(sz.x * 0.5f - 1f, 0f), new Vector2(2f, sz.y), c);
    }

    TextMeshProUGUI Txt(string s, float size, Vector2 center, Vector2 box)
    {
        var rt = new GameObject("t", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(marks, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(center.x, -center.y); rt.sizeDelta = box;
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>(); if (font != null) t.font = font;
        t.text = s; t.fontSize = size; t.characterSpacing = 8f; t.color = new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.75f);
        t.alignment = TextAlignmentOptions.Center; t.enableWordWrapping = false; t.raycastTarget = false;
        return t;
    }

    static Sprite ring;
    static Sprite RingSprite()
    {
        if (ring != null) return ring;
        int n = 64; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
            float a = Mathf.Clamp01(1f - Mathf.Abs(d - (n / 2f - 4f)) / 2.5f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        return ring = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}
