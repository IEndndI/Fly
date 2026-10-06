using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 游戏里右上角的快捷栏（分镜 #gquick）：01 Auto · 02 Skip · 03 Log · 04 Save · 05 Menu。
// 686 宽、5 格、格距 24，从 x=1114 起；每格一条细下划线，开着的（Auto / Skip）下划线加粗。
// 夜里换纸色（跟 GameChrome 一起变）；书页散开到 3 级变淡、4 级消失。自动播放锁住时 Auto / Skip 不能开。
// 03–05 和分镜一样打开系统页：Log=回想、Save=存档页、Menu=设置页（2026-10-06）。
public class QuickMenu : MonoBehaviour
{
    public DialogueManager dialogue;
    public TMP_FontAsset font;                   // MainFont
    public TMP_FontAsset textFont;               // 回想里正文的字体（WenKai）

    static readonly Color Cobalt = new Color(0f, 0.278f, 0.671f);
    static readonly Color Paper = new Color(0.949f, 0.925f, 0.875f);
    static readonly string[] Names = { "Auto", "Skip", "Log", "Save", "Menu" };

    TextMeshProUGUI[] labels = new TextMeshProUGUI[5];
    Image[] rules = new Image[5];
    CanvasGroup group;

    void Awake()
    {
        if (dialogue == null) dialogue = FindObjectOfType<DialogueManager>();
        var root = (RectTransform)transform;
        root.anchorMin = root.anchorMax = new Vector2(1f, 1f); root.pivot = new Vector2(1f, 1f);   // 贴右缘（宽屏手机上不会悬在半空）
        root.anchoredPosition = new Vector2(-120f, -28f); root.sizeDelta = new Vector2(686f, 28f);
        group = gameObject.AddComponent<CanvasGroup>();
        float w = (686f - 4 * 24f) / 5f;
        for (int i = 0; i < 5; i++)
        {
            int k = i;
            var go = new GameObject(Names[i], typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(root, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(i * (w + 24f), 0f); rt.sizeDelta = new Vector2(w, 28f);
            go.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            go.GetComponent<Button>().onClick.AddListener(() => Press(k));
            var t = new GameObject("T", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.rectTransform.SetParent(rt, false); t.rectTransform.anchorMin = new Vector2(0f, 1f); t.rectTransform.anchorMax = new Vector2(1f, 1f);
            t.rectTransform.pivot = new Vector2(0f, 1f); t.rectTransform.anchoredPosition = Vector2.zero; t.rectTransform.sizeDelta = new Vector2(0f, 16f);
            if (font != null) t.font = font;
            t.text = "<alpha=#80>0" + (i + 1) + "</alpha>  " + Names[i].ToUpper(); t.fontSize = 12f; t.characterSpacing = 9f; t.raycastTarget = false;
            labels[i] = t;
            var r = new GameObject("Rule", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            r.rectTransform.SetParent(rt, false); r.rectTransform.anchorMin = new Vector2(0f, 0f); r.rectTransform.anchorMax = new Vector2(1f, 0f);
            r.rectTransform.pivot = new Vector2(0.5f, 0f); r.rectTransform.anchoredPosition = new Vector2(0f, 4f); r.rectTransform.sizeDelta = new Vector2(0f, 1f);
            r.raycastTarget = false; rules[i] = r;
        }
    }

    void Press(int i)
    {
        bool locked = dialogue.autoLock != null && dialogue.autoLock.IsLocked;
        switch (i)
        {
            case 0: if (locked) { dialogue.autoLock.Nudge(); return; } DialogueManager.autoMode = !DialogueManager.autoMode; DialogueManager.skipMode = false; Sfx("Fly/sfx_tick"); break;
            case 1: if (locked) { dialogue.autoLock.Nudge(); return; } DialogueManager.skipMode = !DialogueManager.skipMode; DialogueManager.autoMode = false; Sfx("Fly/sfx_tick"); break;
            case 2: DialogueManager.autoMode = DialogueManager.skipMode = false; OpenSysPage("log"); break;
            case 3: DialogueManager.autoMode = DialogueManager.skipMode = false; OpenSysPage("save"); break;
            case 4: DialogueManager.autoMode = DialogueManager.skipMode = false; OpenSysPage("settings"); break;
        }
    }

    void OpenSysPage(string tab)   // 和分镜一样：03 Log / 04 Save / 05 Menu 都开系统页
    {
        SysPanels.UiFont = font; SysPanels.TextFont = textFont;
        SysPanels.Show(tab);
    }

    void Sfx(string n) { if (dialogue.audioPlayer != null) dialogue.audioPlayer.PlaySfx(n); }

    void Update()
    {
        // 和 GameChrome 一起：有台词以后才出现；夜里纸色；书页散开时退场
        float target = dialogue.CurrentIndex < 0 ? 0f : PageFx.Level >= 4 ? 0f : PageFx.Level == 3 ? 0.25f : 1f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, Time.deltaTime / 0.4f);
        group.interactable = group.blocksRaycasts = group.alpha > 0.2f;
        Color c = Color.Lerp(Cobalt, Paper, GameChrome.DarkK);
        for (int i = 0; i < 5; i++)
        {
            bool on = (i == 0 && DialogueManager.autoMode) || (i == 1 && DialogueManager.skipMode);
            labels[i].color = c;
            rules[i].color = new Color(c.r, c.g, c.b, on ? 1f : 0.3f);
            rules[i].rectTransform.sizeDelta = new Vector2(0f, on ? 4f : 1f);
        }
    }
}
