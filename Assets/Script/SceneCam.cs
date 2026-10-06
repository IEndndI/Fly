using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 场景镜头（第二章飞行段用，2026-10-06）：把「世界图层」——背景 + 背景线稿——当成一台 2D 摄像机，
// 缓慢地推近 / 上下平移。分镜里第二章的那一段本来是一整条不切镜的镜头（lines 6–19 共用一个飞行时钟），
// 这边是几张静态图切换，靠这台镜头一直慢慢动，做出「一镜到底」的感觉。
//
// 表格用法（effect 列，随便写几列都行，顺序随意）：
//     cam:z(1.12):y(-48):t(9)      → 9 秒内缓动推到 1.12 倍、画面上移 48（露出下方更多天空）
//     cam:z(1) 或 cam:reset        → 慢慢回到原位
//     参数：z = 目标缩放（≥1）、x / y = 目标平移（设计像素，+x 右、+y 上）、t = 走多久（秒，默认 8）
//     ——数字时长也认：cam:9 等于 t(9)。
//
// 两条规矩（静态图放大缩小必然要管的）：
//   1. z 只放大不缩小（<1 会在画布边上露出空白），平移量按「多出来的边」自动夹住，怎么推都不会穿帮；
//   2. 命令是「不等」的：镜头自己慢慢走，文字照常翻页；下一条 cam 从当前姿势接着走——
//      镜头永远不跳，这就是一镜到底的关键。
public class SceneCam : MonoBehaviour
{
    public static SceneCam instance;

    public Image backgroundImage;          // Canvas/Background（留空自己找）
    public Image backgroundLine;           // Canvas/BG_Line（背景的线稿层）
    readonly List<RectTransform> layers = new List<RectTransform>();

    float zoom = 1f, zFrom = 1f, zTo = 1f;
    Vector2 pan = Vector2.zero, pFrom, pTo;
    float moveT = -1f, moveDur = 1f;       // moveT < 0 = 没在走
    float zAt = 1f; Vector2 pAt;           // 当前姿势（每帧演算出来的）

    void Awake()
    {
        instance = this;
        if (backgroundImage == null)
        {
            var go = GameObject.Find("Canvas/Background");
            if (go != null) backgroundImage = go.GetComponent<Image>();
        }
        if (backgroundLine == null)
        {
            var go = GameObject.Find("Canvas/BG_Line");
            if (go != null) backgroundLine = go.GetComponent<Image>();
        }
        // 世界图层统一成「居中、1920×1080」的坐标，镜头才好算（GameChrome 的宽屏 cover 也是这么摆的）
        foreach (var rt in new RectTransform[] { backgroundImage != null ? backgroundImage.rectTransform : null, backgroundLine != null ? backgroundLine.rectTransform : null })
        {
            if (rt == null) continue;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1920f, 1080f);
            rt.anchoredPosition = Vector2.zero;
            layers.Add(rt);
        }
        pAt = pan; zAt = zoom;
    }

    // 表格命令入口（EffectPlayer 调的）
    public void Command(Command cmd)
    {
        if (cmd.arg == "reset") { Move(1f, Vector2.zero, GetArg(cmd, "t", 8f)); return; }
        float z = GetArg(cmd, "z", 1f);
        float x = GetArg(cmd, "x", 0f);
        float y = GetArg(cmd, "y", 0f);
        float t = cmd.duration >= 0f ? cmd.duration : GetArg(cmd, "t", 8f);   // 手写数字时长优先
        Move(z, new Vector2(x, y), t);
    }

    // 从当前姿势接着走向新姿势（一镜到底：镜头不跳）
    public void Move(float z, Vector2 p, float time)
    {
        zFrom = zAt; pFrom = pAt;
        zTo = Mathf.Max(1f, z); pTo = p;
        moveDur = Mathf.Max(0.01f, time);
        moveT = 0f;
    }

    // 立刻回到原位（跳转 / 读档用：免得把别的章的镜头姿势带过去）
    public void SnapHome()
    {
        zoom = zFrom = zTo = 1f; pan = pFrom = pTo = Vector2.zero;
        pAt = Vector2.zero; zAt = 1f; moveT = -1f;
    }

    void LateUpdate()
    {
        if (layers.Count == 0) return;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var crt = (RectTransform)canvas.transform;
        float cw = crt.rect.width, ch = crt.rect.height;
        if (cw < 1f) return;

        if (moveT >= 0f)
        {
            moveT += Time.deltaTime;
            float u = Mathf.Clamp01(moveT / moveDur);
            float e = u < 0.5f ? 4f * u * u * u : 1f - Mathf.Pow(-2f * u + 2f, 3f) / 2f;   // eIO
            zAt = Mathf.Lerp(zFrom, zTo, e);
            pAt = Vector2.Lerp(pFrom, pTo, e);
            if (u >= 1f) moveT = -1f;
        }

        float cover = Mathf.Max(cw / 1920f, ch / 1080f);          // 和 GameChrome 的宽屏 cover 一致
        float s = cover * zAt;
        float sx = (1920f * s - cw) * 0.5f, sy = (1080f * s - ch) * 0.5f;   // 多出来的边（夹住平移量）
        var p = new Vector2(Mathf.Clamp(pAt.x, -sx, sx), Mathf.Clamp(pAt.y, -sy, sy));
        foreach (var rt in layers)
        {
            rt.localScale = new Vector3(s, s, 1f);
            rt.anchoredPosition = p;
        }
    }

    float GetArg(Command cmd, string key, float def)
    {
        string text;
        if (cmd.namedArgs.TryGetValue(key, out text))
        {
            float v;
            if (float.TryParse(text, out v)) return v;
            Debug.LogWarning("镜头参数读不出来：" + key + "=" + text + "，用默认值 " + def);
        }
        return def;
    }
}
