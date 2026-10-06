using UnityEngine;
using UnityEngine.UI;

// 换背景时不再硬切：旧背景（水彩底 + 蜡笔线）留一张快照压在上面，1.2 秒淡出；
// 新背景同时从 1.04 倍缓缓收回原位（借 SceneCam 的镜头），像一页纸落定。DialogueManager 换背景前调 Snapshot。
public class BgFade : MonoBehaviour
{
    static BgFade inst;
    Image prevBase, prevLine; BoilSprite prevBoil; CanvasGroup group; float t = -1f;
    public float fadeTime = 1.2f;

    public static void Snapshot(Image bg, BoilSprite line)
    {
        if (bg == null || bg.sprite == null) return;
        if (inst == null)
        {
            var go = new GameObject("BgFade", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(bg.transform.parent, false);
            inst = go.AddComponent<BgFade>();
            inst.Build(bg, line);
        }
        inst.Take(bg, line);
    }

    void Build(Image bg, BoilSprite line)
    {
        var r = (RectTransform)transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        int after = line != null ? line.transform.GetSiblingIndex() : bg.transform.GetSiblingIndex();
        transform.SetSiblingIndex(after + 1);                                   // 压在新背景和它的线稿上面
        group = GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.alpha = 0f;
        prevBase = MakeImg("PrevBase"); prevLine = MakeImg("PrevLine");
        prevBoil = prevLine.gameObject.AddComponent<BoilSprite>();
    }

    Image MakeImg(string n)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var im = go.GetComponent<Image>(); im.raycastTarget = false; return im;
    }

    void Take(Image bg, BoilSprite line)
    {
        Copy(bg.rectTransform, prevBase.rectTransform); prevBase.sprite = bg.sprite; prevBase.color = bg.color;
        bool hasLine = line != null && line.frames != null && line.frames.Length > 0 && line.GetComponent<Image>().enabled;
        prevLine.enabled = hasLine;
        if (hasLine) { Copy(line.GetComponent<RectTransform>(), prevLine.rectTransform); prevBoil.SetFrames(line.frames); }
        group.alpha = 1f; t = 0f;
        if (SceneCam.instance != null) SceneCam.instance.Settle(1.04f, 2.6f);   // 新背景的一点点运动
    }

    static void Copy(RectTransform from, RectTransform to)
    {
        to.anchorMin = from.anchorMin; to.anchorMax = from.anchorMax; to.pivot = from.pivot;
        to.sizeDelta = from.sizeDelta; to.anchoredPosition = from.anchoredPosition; to.localScale = from.localScale;
    }

    void Update()
    {
        if (t < 0f) return;
        t += Time.deltaTime;
        float u = Mathf.Clamp01(t / fadeTime);
        group.alpha = 1f - (u < 0.5f ? 2f * u * u : 1f - Mathf.Pow(-2f * u + 2f, 2f) / 2f);
        if (u >= 1f) { t = -1f; group.alpha = 0f; }
    }
}
