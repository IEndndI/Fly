using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// v1.1 屏幕比例适配（手机 20:9、平板 4:3 都不错位）：
// 整套界面都是按 1920×1080、左上角原点摆的。场景一载入，就在 Canvas 下建一个居中的 1920×1080「Frame」，
// 把 Canvas 原来的子物体全部搬进去——于是在任何比例上，界面都和 16:9 时一模一样、而且居中。
// 多出来的两边（或上下）交给：背景 cover 铺满（SceneCam / GameChrome）、Bleed（整屏色块伸到屏幕边）、
// CoverUV（视频裁切铺满，不拉伸）、PrintMargin（标题场景两边是印张的纸边：裁切线、套准十字、色标）。
public static class UIFrame
{
    public const float W = 1920f, H = 1080f;
    public static bool ForceTouch;                                  // 编辑器里预览手机版界面用
#if UNITY_EDITOR
    public static bool Touch => Application.isMobilePlatform || ForceTouch || UnityEditor.EditorPrefs.GetBool("Fly.PreviewTouch", false);   // 菜单 Tools/预览手机版按钮
#else
    public static bool Touch => Application.isMobilePlatform || ForceTouch;   // 手机 / 平板：按钮放大
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook() { SceneManager.sceneLoaded += (s, m) => AdoptAll(s); }

    static void AdoptAll(Scene s)
    {
        foreach (var go in s.GetRootGameObjects())
        {
            var scaler = go.GetComponent<CanvasScaler>();
            if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;    // 画布 ≥ 1920×1080，谁都不裁
            Adopt((RectTransform)go.transform);
        }
    }

    static void Adopt(RectTransform canvas)
    {
        if (canvas.Find("Frame") != null) return;
        var kids = new List<Transform>();
        foreach (Transform c in canvas) kids.Add(c);
        var frame = new GameObject("Frame", typeof(RectTransform)).GetComponent<RectTransform>();
        frame.SetParent(canvas, false);
        frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
        frame.anchoredPosition = Vector2.zero; frame.sizeDelta = new Vector2(W, H);
        frame.SetSiblingIndex(0);
        foreach (var c in kids) c.SetParent(frame, false);              // 保留各自的锚点和坐标：相对 1920×1080 摆，和 16:9 一样
        foreach (var n in new[] { "FlashOverlay", "BlankPage", "DreamFx/Edge", "EarthFX/Bloom", "EarthFX/Aurora", "GameChrome/Progress" })
        {
            var t = frame.Find(n);
            if (t != null && t.GetComponent<Bleed>() == null) t.gameObject.AddComponent<Bleed>();
        }
    }

    // 场景里按名字找界面物体（搬进 Frame 前后都能找到）
    public static GameObject Find(string name)
    {
        return GameObject.Find("Canvas/Frame/" + name) ?? GameObject.Find("Canvas/" + name);
    }

    // 两边各多出多少（以父物体为准：父物体就是整块画布时为 0）
    public static Vector2 Extra(RectTransform rt)
    {
        var canvas = rt.GetComponentInParent<Canvas>();
        if (canvas == null) return Vector2.zero;
        var c = ((RectTransform)canvas.rootCanvas.transform).rect;
        return new Vector2(Mathf.Max(0f, (c.width - W) * 0.5f), Mathf.Max(0f, (c.height - H) * 0.5f));
    }
}

// 整屏铺满的东西（闪白、白纸页、视频、色页）：锚点拉满的同时，四边再往外伸到屏幕边缘
public class Bleed : MonoBehaviour
{
    RectTransform rt;
    void LateUpdate()
    {
        if (rt == null) rt = (RectTransform)transform;
        var p = rt.parent as RectTransform; if (p == null) return;
        var canvas = rt.GetComponentInParent<Canvas>(); if (canvas == null) return;
        var c = ((RectTransform)canvas.rootCanvas.transform).rect;
        float bx = Mathf.Max(0f, (c.width - p.rect.width) * 0.5f), by = Mathf.Max(0f, (c.height - p.rect.height) * 0.5f);
        // 只往「本来就拉满」的方向伸（顶边的进度条只横向伸，不会被拉成整屏）
        bool fx = rt.anchorMin.x <= 0f && rt.anchorMax.x >= 1f, fy = rt.anchorMin.y <= 0f && rt.anchorMax.y >= 1f;
        var mn = rt.offsetMin; var mx = rt.offsetMax;
        if (fx) { mn.x = -bx; mx.x = bx; }
        if (fy) { mn.y = -by; mx.y = by; }
        rt.offsetMin = mn; rt.offsetMax = mx;
    }
}

// 视频：按自己的比例裁切铺满（cover），绝不拉伸
[RequireComponent(typeof(RawImage))]
public class CoverUV : MonoBehaviour
{
    public float aspect = 16f / 9f;
    RawImage img;
    void LateUpdate()
    {
        if (img == null) img = GetComponent<RawImage>();
        var r = img.rectTransform.rect; if (r.width < 1f || r.height < 1f) return;
        float a = r.width / r.height;
        img.uvRect = a > aspect ? new Rect(0f, (1f - aspect / a) * 0.5f, 1f, aspect / a)      // 比视频宽：裁上下
                                : new Rect((1f - a / aspect) * 0.5f, 0f, a / aspect, 1f);     // 比视频窄：裁左右
    }
}
