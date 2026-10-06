using UnityEngine;
using TMPro;

// 「书中」的句子（分镜 #gstrip.book）：对话纸条在 0.5 秒里展开成一整页书——上沿从 808 升到 216，
// 字变大、行距变宽、往里收，左上印「书中 · In the book · 第 7 章」，右下印页码 p. 112；换成别的类型再收回去。
// 读 DialogueManager.CurrentLineType，不用填表。挂在场景里任意物体上（这里挂在 Canvas 下的空物体 BookMode）。
public class BookMode : MonoBehaviour
{
    public DialogueManager dialogue;
    public RectTransform panel;                 // 对话框（留空 = 文本的父物体 Panel）
    public TMP_FontAsset labelFont;             // 小字的字体（MainFont）
    public string bookLabel = "书中 · IN THE BOOK · 第 7 章";
    public string pageLabel = "p. 112";

    static readonly Color Cobalt = new Color(0f, 0.278f, 0.671f);
    const float NormalH = 192f, BookH = 784f;

    TextMeshProUGUI text, mode, labelL, labelP;
    Vector2 textMin, textMax; float textSize, textSpacing;
    float k, kLabel;                            // 0 = 纸条，1 = 整页书

    void Start()
    {
        if (dialogue == null) dialogue = FindObjectOfType<DialogueManager>();
        text = dialogue.dialogueText;
        if (panel == null) panel = (RectTransform)text.transform.parent;
        var rt = text.rectTransform; textMin = rt.offsetMin; textMax = rt.offsetMax; textSize = text.fontSize; textSpacing = text.lineSpacing;
        var m = panel.Find("ModeLabel"); if (m != null) mode = m.GetComponent<TextMeshProUGUI>();
        labelL = Label("BookLabel", bookLabel, new Vector2(0f, 1f), new Vector2(96f, -56f), TextAlignmentOptions.TopLeft);
        labelP = Label("BookPage", pageLabel, new Vector2(1f, 0f), new Vector2(-56f, 40f), TextAlignmentOptions.BottomRight);
    }

    void Update()
    {
        if (text == null) return;
        bool book = dialogue.CurrentLineType == "书中";
        k = Mathf.MoveTowards(k, book ? 1f : 0f, Time.deltaTime / 0.5f);
        float e = book ? 1f - Mathf.Pow(1f - k, 4f) : k * k * k * k;        // 展开快起慢收，收回也一样利落
        panel.sizeDelta = new Vector2(panel.sizeDelta.x, Mathf.Lerp(NormalH, BookH, e));
        var rt = text.rectTransform;
        rt.offsetMin = Vector2.Lerp(textMin, new Vector2(96f, 80f), e);
        rt.offsetMax = Vector2.Lerp(textMax, new Vector2(-240f, -120f), e);
        text.fontSize = Mathf.Lerp(textSize, 34f, e);
        text.lineSpacing = Mathf.Lerp(textSpacing, 30f, e);
        if (mode != null) mode.alpha = 0.55f * (1f - e);
        // 两行小字：展开过一半（0.25 秒）以后再淡入
        kLabel = Mathf.MoveTowards(kLabel, book && k > 0.5f ? 1f : 0f, Time.deltaTime / 0.3f);
        labelL.alpha = labelP.alpha = kLabel;
    }

    TextMeshProUGUI Label(string n, string s, Vector2 anchor, Vector2 pos, TextAlignmentOptions al)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(panel, false);
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(600f, 18f);
        var t = go.GetComponent<TextMeshProUGUI>(); if (labelFont != null) t.font = labelFont;
        t.text = s; t.fontSize = 12f; t.characterSpacing = 9f; t.color = Cobalt; t.alignment = al;
        t.enableWordWrapping = false; t.raycastTarget = false; t.alpha = 0f;
        return t;
    }
}
