using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 「他醒了。」：画面硬切成一张白纸，两秒的安静之后这句话才浮出来；下一句时白纸慢慢淡回房间。
// 这期间点击不跳过、不加速（DialogueManager.blockUntil）。
// 表格 effect 列：那一句写 blank，下一句写 unblank。界面零件在 Awake 里自己生成，挂到 Canvas 下一个空物体上即可（放在对话框上面）。
public class BlankPage : MonoBehaviour
{
    public float holdTime = 2.9f;       // 白纸至少停多久才能点
    public float textIn = 2.0f;         // 字从第几秒开始浮出来
    public float textFade = 0.6f;
    public float outTime = 1.4f;        // 淡回房间
    public TMP_FontAsset font;

    static readonly Color Cobalt = new Color(0f, 0.278f, 0.671f);
    static readonly Color Paper = new Color(0.949f, 0.925f, 0.875f);

    CanvasGroup group; TextMeshProUGUI text; Coroutine run;

    void Awake()
    {
        var rt = (RectTransform)transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = GetComponent<Image>(); if (img == null) img = gameObject.AddComponent<Image>();
        img.color = Paper; img.raycastTarget = false;
        group = GetComponent<CanvasGroup>(); if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false; group.alpha = 0f;
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        var tr = go.GetComponent<RectTransform>(); tr.SetParent(rt, false);
        tr.anchorMin = new Vector2(0f, 0.5f); tr.anchorMax = new Vector2(1f, 0.5f); tr.pivot = new Vector2(0.5f, 0.5f);
        tr.anchoredPosition = new Vector2(0f, 540f - 532f); tr.sizeDelta = new Vector2(0f, 40f);   // HTML：top 512，行高 40
        text = go.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = 30f; text.color = Cobalt; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        text.alpha = 0f;
    }

    public void Show(string line)
    {
        if (run != null) StopCoroutine(run);
        run = StartCoroutine(ShowRun(line));
    }

    public void Hide()
    {
        if (group.alpha <= 0f) return;
        if (run != null) StopCoroutine(run);
        run = StartCoroutine(HideRun());
    }

    IEnumerator ShowRun(string line)
    {
        text.text = line; text.alpha = 0f; group.alpha = 1f;          // 硬切：没有过渡
        DialogueManager.blockUntil = Time.time + holdTime;
        yield return new WaitForSeconds(textIn);
        for (float t = 0f; t < textFade; t += Time.deltaTime) { text.alpha = t / textFade; yield return null; }
        text.alpha = 1f; run = null;
    }

    IEnumerator HideRun()
    {
        DialogueManager.blockUntil = Time.time + outTime;
        for (float t = 0f; t < outTime; t += Time.deltaTime)
        {
            float u = t / outTime; group.alpha = 1f - (u < 0.5f ? 2f * u * u : 1f - Mathf.Pow(-2f * u + 2f, 2f) / 2f);
            yield return null;
        }
        group.alpha = 0f; run = null;
    }
}
