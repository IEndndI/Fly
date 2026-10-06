using UnityEngine;
using UnityEngine.UI;

// 第四章的梦：四周一圈会呼吸的暗角和虚线（dream_edge），天上的鸟群和滑翔机群一直在漂（各分往右 / 往左两层），
// 还有那一架近在咫尺飞过去的滑翔机。表格 effect 列：
//   dream:on        进梦（暗角出现）        dream:birds    「然后他看见了很多滑翔机」：鸟群淡入
//   dream:pass      那一架飞过             dream:gliders  「交织成网」：滑翔机群淡入
//   dream:off       醒来（全部收起）
// 界面零件在 Awake 里自己生成：挂到 Canvas 下背景线稿上面的一个空物体上。
public class DreamFx : MonoBehaviour
{
    [Header("漂移速度（像素/秒，来自分镜导出时的平均值）")]
    public float birdsSpeed = 11f, glidersRight = 29f, glidersLeft = 25f;

    class Drift { public RectTransform a, b; public float speed; public CanvasGroup g; public float target; }
    CanvasGroup root, edge;
    Drift[] drifts;
    RectTransform pass; CanvasGroup passG; float passT = -1f;
    float on, t;

    void Awake()
    {
        var rt = (RectTransform)transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        root = gameObject.AddComponent<CanvasGroup>(); root.blocksRaycasts = false; root.alpha = 0f;

        drifts = new[] {
            MakeDrift("flock_birds_r", birdsSpeed), MakeDrift("flock_birds_l", -birdsSpeed),
            MakeDrift("flock_gliders_r", glidersRight), MakeDrift("flock_gliders_l", -glidersLeft) };

        // 飞过的那一架：轴心 = 分镜里 drawItems 的原点（导出时记下的 517, 119 / 1039 × 253）
        var go = new GameObject("Pass", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        pass = go.GetComponent<RectTransform>(); pass.SetParent(rt, false);
        pass.anchorMin = pass.anchorMax = new Vector2(0f, 1f); pass.sizeDelta = new Vector2(1039f, 253f);
        pass.pivot = new Vector2(517.2f / 1039f, 1f - 119.4f / 253f);
        go.GetComponent<Image>().raycastTarget = false;
        go.AddComponent<BoilSprite>().SetFrames(Frames("glider_pass"));
        passG = go.GetComponent<CanvasGroup>(); passG.alpha = 0f;

        var e = new GameObject("Edge", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        var er = e.GetComponent<RectTransform>(); er.SetParent(rt, false);
        er.anchorMin = Vector2.zero; er.anchorMax = Vector2.one; er.offsetMin = er.offsetMax = Vector2.zero;
        var ei = e.GetComponent<Image>(); ei.sprite = Resources.Load<Sprite>("Sprites/Fly/FX/dream_edge"); ei.raycastTarget = false;
        edge = e.GetComponent<CanvasGroup>();
    }

    public void Command(string arg)
    {
        switch (arg)
        {
            case "on": on = 1f; t = 0f; foreach (var d in drifts) d.g.alpha = d.target = 0f; break;
            case "birds": drifts[0].target = drifts[1].target = 1f; break;
            case "gliders": drifts[2].target = drifts[3].target = 1f; break;
            case "pass": passT = 0f; break;
            case "off": on = 0f; root.alpha = 0f; passT = -1f; passG.alpha = 0f; break;
            default: Debug.LogWarning("dream: 不认识的参数 " + arg); break;
        }
    }

    void Update()
    {
        root.alpha = Mathf.MoveTowards(root.alpha, on, Time.deltaTime / 0.6f);
        if (root.alpha <= 0f) return;
        t += Time.deltaTime;
        edge.alpha = 0.85f + 0.15f * Mathf.Sin(t * 0.6f);                       // 暗角在呼吸
        foreach (var d in drifts)
        {
            d.g.alpha = Mathf.MoveTowards(d.g.alpha, d.target, Time.deltaTime / 2.4f);
            float x = Mathf.Repeat(d.speed * t + 400f, 2800f) - 400f;             // 漂出画面就从另一边回来（和分镜一样 2800 一圈）
            d.a.anchoredPosition = new Vector2(x, 0f); d.b.anchoredPosition = new Vector2(x - 2800f, 0f);
        }
        if (passT >= 0f)
        {
            passT += Time.deltaTime;
            float u = Mathf.Clamp01((passT - 0.3f) / 5.1f), e = u < 0.5f ? 4f * u * u * u : 1f - Mathf.Pow(-2f * u + 2f, 3f) / 2f;
            pass.anchoredPosition = new Vector2(Mathf.Lerp(-620f, 2540f, e), -(500f - Mathf.Sin(u * Mathf.PI) * 90f));
            passG.alpha = u <= 0f || u >= 1f ? 0f : Mathf.Clamp01(u * 7f) * Mathf.Clamp01((1f - u) * 7f);
            if (u >= 1f) passT = -1f;
        }
    }

    Drift MakeDrift(string file, float speed)
    {
        var go = new GameObject(file, typeof(RectTransform), typeof(CanvasGroup));
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var d = new Drift { speed = speed, g = go.GetComponent<CanvasGroup>() };
        d.g.alpha = 0f;
        d.a = Copy(rt, file); d.b = Copy(rt, file);
        return d;
    }

    RectTransform Copy(RectTransform parent, string file)
    {
        var go = new GameObject("copy", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f); rt.sizeDelta = new Vector2(1920f, 1080f);
        go.GetComponent<Image>().raycastTarget = false;
        go.AddComponent<BoilSprite>().SetFrames(Frames(file));
        return rt;
    }

    static Sprite[] Frames(string file)
    {
        var f = new Sprite[3];
        for (int v = 0; v < 3; v++) f[v] = Resources.Load<Sprite>("Sprites/Fly/FX/" + file + "_v" + v);
        return f[0] != null ? f : new Sprite[0];
    }
}
