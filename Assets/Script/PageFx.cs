using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 第 8 幕：书页慢慢散开（分镜 act 8，第三、四章）。一共四级，表格 effect 列写 fx:lv1 … fx:lv4，回到正常写 fx:lv0。
//   1  排版网格浮出来（淡）
//   2  网格更清楚；名牌消失；对话框变薄、往右错开 12
//   3  对话框更薄；画面褪色；四条蜡笔乱涂爬上界面
//   4  对话框几乎透明、往左错开；画面再褪色；所有乱涂都在；页码开始跳号（GameChrome 读 Level）
// 已经画上去的乱涂不会每句重画。界面零件在 Awake 里自己生成：挂到 Canvas 下对话框上面的一个空物体上。
public class PageFx : MonoBehaviour
{
    public static int Level { get; private set; }

    public DialogueManager dialogue;
    public RectTransform textPanel;          // 对话框（留空 = DialogueManager 的文本往上找带 Image 的那层）
    public Image[] tintTargets;              // 要褪色的画面（留空 = 背景 + 背景线稿）

    static readonly Color Cobalt = new Color(0f, 0.278f, 0.671f);
    static readonly float[] GridA = { 0f, 0.5f, 0.9f, 0.9f, 0.9f };
    static readonly float[] PanelA = { 1f, 1f, 0.62f, 0.42f, 0.2f };
    static readonly float[] PanelX = { 0f, 0f, 12f, 12f, -12f };
    static readonly float[] Tint = { 1f, 1f, 1f, 0.9f, 0.8f };

    CanvasGroup grid, scribA, scribB, plate;
    Image panelImg; Color panelBase; Vector2 panelHome;
    float gridA, panelA = 1f, panelX, tint = 1f;

    void Awake()
    {
        if (dialogue == null) dialogue = FindObjectOfType<DialogueManager>();
        var root = (RectTransform)transform;
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;

        // 排版网格：12 栏的左右边 + 上下两条横线（和分镜 #ggrid 一样）
        grid = Layer("Grid", root);   // 先建在这里，下面找到对话框以后挪到它底下
        for (int i = 0; i < 12; i++) { Line(grid.transform, 120 + i * 142, 80, 1, 920); Line(grid.transform, 120 + i * 142 + 118, 80, 1, 920); }
        Line(grid.transform, 0, 80, 1920, 1); Line(grid.transform, 0, 1000, 1920, 1);
        grid.alpha = 0f;

        scribA = Scrawl("ScrawlA", root, "scrib_a"); scribB = Scrawl("ScrawlB", root, "scrib_b");

        if (textPanel == null && dialogue != null)
        {
            Transform t = dialogue.dialogueText.transform;
            while (t != null && t.GetComponent<Image>() == null) t = t.parent;
            if (t != null) textPanel = (RectTransform)t;
        }
        if (textPanel != null)
        {
            // 对话框里那张纸条（Strip）才变薄；整个对话框（连同字）一起错开
            Transform strip = textPanel.Find("Strip");
            panelImg = strip != null ? strip.GetComponent<Image>() : textPanel.GetComponent<Image>();
            panelBase = panelImg.color; panelHome = textPanel.anchoredPosition;
            Transform np = textPanel.Find("NamePlate");
            if (np != null) { plate = np.GetComponent<CanvasGroup>(); if (plate == null) plate = np.gameObject.AddComponent<CanvasGroup>(); }
            // 网格在对话框底下（分镜 #ggrid 在界面最底层）
            grid.transform.SetParent(textPanel.parent, false);
            grid.transform.SetSiblingIndex(textPanel.GetSiblingIndex());
        }
        if (tintTargets == null || tintTargets.Length == 0)
        {
            var bg = UIFrame.Find("Background"); var line = UIFrame.Find("BG_Line");
            tintTargets = new[] { bg ? bg.GetComponent<Image>() : null, line ? line.GetComponent<Image>() : null };
        }
        Level = 0;
    }

    public void Set(int lv)
    {
        lv = Mathf.Clamp(lv, 0, 4);
        if (lv >= 3 && Level < 3) scribA.alpha = 0.001f;    // 刚升到 3：开始画（Update 里淡入）
        if (lv >= 4 && Level < 4) scribB.alpha = 0.001f;
        if (lv < 3) scribA.alpha = 0f;
        if (lv < 4) scribB.alpha = 0f;
        Level = lv;
    }

    void Update()
    {
        float k = Time.deltaTime / 0.6f;
        gridA = Mathf.MoveTowards(gridA, GridA[Level], k); grid.alpha = gridA;
        panelA = Mathf.MoveTowards(panelA, PanelA[Level], k);
        panelX = Mathf.MoveTowards(panelX, PanelX[Level], Time.deltaTime * 40f);
        tint = Mathf.MoveTowards(tint, Tint[Level], k * 0.3f);
        if (panelImg != null) { panelImg.color = new Color(panelBase.r, panelBase.g, panelBase.b, panelBase.a * panelA * SaveSystem.Box); textPanel.anchoredPosition = panelHome + new Vector2(panelX, 0f); }   // SaveSystem.Box：设置里的「对话条」不透明度
        if (plate != null) plate.alpha = Mathf.MoveTowards(plate.alpha, Level >= 2 ? 0f : 1f, k);
        foreach (var img in tintTargets) if (img != null) img.color = new Color(tint, tint, Mathf.Min(1f, tint + 0.04f), img.color.a);
        if (scribA.alpha > 0f) scribA.alpha = Mathf.MoveTowards(scribA.alpha, 1f, Time.deltaTime / 0.8f);
        if (scribB.alpha > 0f) scribB.alpha = Mathf.MoveTowards(scribB.alpha, 1f, Time.deltaTime / 1.6f);
    }

    CanvasGroup Layer(string n, Transform parent)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(CanvasGroup));
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var g = go.GetComponent<CanvasGroup>(); g.blocksRaycasts = false; g.interactable = false;
        return g;
    }

    void Line(Transform parent, float x, float y, float w, float h)
    {
        var go = new GameObject("l", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        var img = go.GetComponent<Image>(); img.color = new Color(Cobalt.r, Cobalt.g, Cobalt.b, 0.16f); img.raycastTarget = false;
    }

    CanvasGroup Scrawl(string n, Transform parent, string file)
    {
        var g = Layer(n, parent); g.alpha = 0f;
        g.gameObject.AddComponent<Image>().raycastTarget = false;
        var frames = new Sprite[3];
        for (int v = 0; v < 3; v++) frames[v] = Resources.Load<Sprite>("Sprites/Fly/FX/" + file + "_v" + v);
        g.gameObject.AddComponent<BoilSprite>().SetFrames(frames[0] != null ? frames : new Sprite[0]);
        return g;
    }
}
