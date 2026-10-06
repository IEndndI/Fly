using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 标题「关于」（通关后出现）：钴蓝底 + 纸色版面，左栏大标题，右栏正文。
// 正文读 Assets/Data/Resources/About.txt（UTF-8）——作者改这个文件，游戏里就跟着变。滚轮可以上下翻；Esc / 右键 / 「返回」关掉。
public class AboutPanel : MonoBehaviour
{
    static readonly Color Cobalt = new Color(0f, 0.278f, 0.671f);
    static readonly Color Paper = new Color(0.949f, 0.925f, 0.875f);

    TMP_FontAsset uiFont, textFont, labelFont;
    CanvasGroup group; RectTransform sheet; float t; bool closing;

    public static void Open(Transform canvas, TMP_FontAsset ui, TMP_FontAsset text, TMP_FontAsset label)
    {
        var go = new GameObject("AboutPanel", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(canvas, false);
        go.transform.SetAsLastSibling();
        var p = go.AddComponent<AboutPanel>();
        p.uiFont = ui; p.textFont = text; p.labelFont = label;
        p.Build();
        SysPanels.UiOpen++;                                                  // 标题菜单借这个判断「有页开着」
    }

    void Build()
    {
        var root = (RectTransform)transform;
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
        group = GetComponent<CanvasGroup>(); group.alpha = 0f;
        var bg = gameObject.AddComponent<Image>(); bg.color = Cobalt;        // 整屏钴蓝，也挡住下面的点击

        // 1920×1080 的版面，居中；纸 1440×840
        var stage = R("Stage", root, Vector2.zero, new Vector2(1920, 1080));
        stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f); stage.anchoredPosition = Vector2.zero;
        sheet = R("Sheet", stage, new Vector2(240, 120), new Vector2(1440, 840));
        Img(sheet, Vector2.zero, new Vector2(1440, 840), Paper);

        // 左栏（x 64–424）：标题
        T(sheet, "关于", labelFont != null ? labelFont : uiFont, 72f, new Vector2(64, 64), new Vector2(360, 90), Cobalt, 0f, TextAlignmentOptions.TopLeft);
        T(sheet, "ABOUT · 05", uiFont, 13f, new Vector2(64, 170), new Vector2(360, 18), Cobalt, 9f, TextAlignmentOptions.TopLeft);
        Img(sheet, new Vector2(64, 200), new Vector2(360, 6), Cobalt);
        T(sheet, "作者的话\nA NOTE FROM THE AUTHOR", uiFont, 14f, new Vector2(64, 224), new Vector2(360, 50), new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.7f), 4f, TextAlignmentOptions.TopLeft);
        T(sheet, "飞 FLY", uiFont, 13f, new Vector2(64, 760), new Vector2(360, 18), Cobalt, 9f, TextAlignmentOptions.TopLeft);

        // 栏线
        Img(sheet, new Vector2(488, 64), new Vector2(2, 712), new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.3f));

        // 右栏（x 552–1376）：正文，可滚
        var view = R("View", sheet, new Vector2(552, 64), new Vector2(824, 640));
        view.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        view.gameObject.AddComponent<RectMask2D>();
        var content = R("Content", view, Vector2.zero, new Vector2(824, 0));
        var body = T(content, LoadText(), textFont != null ? textFont : uiFont, 26f, Vector2.zero, new Vector2(824, 0), new Color(0.12f, 0.12f, 0.16f), 0f, TextAlignmentOptions.TopLeft);
        body.enableWordWrapping = true; body.lineSpacing = 28f; body.paragraphSpacing = 18f;
        body.rectTransform.sizeDelta = new Vector2(824, body.GetPreferredValues(body.text, 824, 0).y + 20f);
        content.sizeDelta = new Vector2(824, body.rectTransform.sizeDelta.y);
        var sr = view.gameObject.AddComponent<ScrollRect>();
        sr.content = content; sr.horizontal = false; sr.viewport = view; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 40f;

        // 底线 + 返回
        Img(sheet, new Vector2(552, 728), new Vector2(824, 2), new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.3f));
        T(sheet, "滚轮翻看 · ESC / 右键 关闭", uiFont, 12f, new Vector2(552, 762), new Vector2(500, 18), new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.7f), 4f, TextAlignmentOptions.TopLeft);
        var back = R("Back", sheet, new Vector2(1176, 748), new Vector2(200, 48));
        var bi = back.gameObject.AddComponent<Image>(); bi.color = Cobalt;
        T(back, "返回　<size=11>BACK</size>", uiFont, 18f, Vector2.zero, new Vector2(200, 48), Paper, 2f, TextAlignmentOptions.Center);
        back.gameObject.AddComponent<Button>().onClick.AddListener(Close);
    }

    static string LoadText()
    {
        var ta = Resources.Load<TextAsset>("About");
        string s = ta != null ? ta.text.Replace("\r", "").Trim() : "";
        return s.Length > 0 ? s : "（作者还在写。）";
    }

    void Update()
    {
        if (!closing)
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / 0.35f);
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) Close();
        }
        else t = Mathf.Max(0f, t - Time.unscaledDeltaTime / 0.25f);
        float e = 1f - Mathf.Pow(1f - t, 3f);
        group.alpha = e;
        sheet.anchoredPosition = new Vector2(240, -120 - 40f * (1f - e));
        if (closing && t <= 0f) Destroy(gameObject);
    }

    void Close()
    {
        if (closing) return;
        closing = true; group.blocksRaycasts = false;
        SysPanels.UiOpen = Mathf.Max(0, SysPanels.UiOpen - 1);
    }

    void OnDestroy() { if (!closing) SysPanels.UiOpen = Mathf.Max(0, SysPanels.UiOpen - 1); }

    // AE 坐标（左上角原点，y 向下）
    static RectTransform R(string n, RectTransform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(n, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(pos.x, -pos.y); rt.sizeDelta = size;
        return rt;
    }

    static Image Img(RectTransform parent, Vector2 pos, Vector2 size, Color c)
    {
        var im = R("Img", parent, pos, size).gameObject.AddComponent<Image>(); im.color = c; im.raycastTarget = false; return im;
    }

    static TextMeshProUGUI T(RectTransform parent, string s, TMP_FontAsset f, float size, Vector2 pos, Vector2 box, Color c, float spacing, TextAlignmentOptions al)
    {
        var t = R("T", parent, pos, box).gameObject.AddComponent<TextMeshProUGUI>();
        if (f != null) t.font = f;
        t.text = s; t.fontSize = size; t.color = c; t.characterSpacing = spacing; t.alignment = al;
        t.enableWordWrapping = false; t.raycastTarget = false;
        return t;
    }
}
