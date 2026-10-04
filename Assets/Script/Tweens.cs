using System;              // Action、Func 在这里
using System.Collections;
using UnityEngine;

// 通用缓动：在 time 秒内把进度 0→1 按曲线 ease 走完，每帧把进度交给 onUpdate
public static class Tweens
{
    public static IEnumerator Tween(float time, Func<float, float> ease, Action<float> onUpdate)
    {
        float elapsed = 0f;
        while (elapsed < time)
        {
            elapsed += Time.deltaTime;                        // 空1
            float t = Mathf.Clamp01(elapsed / time);     // 空2：线性进度 0~1
            onUpdate(ease(t));
            yield return null;
        }
        onUpdate(ease(1f));                            // 空3：保证最后精确停在终点
    }

    // 曲线：冲出去很快、尾巴很长（HTML 原型里的 ease-out）
    public static float EaseOut(float t) { return 1f - Mathf.Pow(1f - t, 3f); }

        // CSS 同款 cubic-bezier(x1, y1, x2, y2)：返回一条曲线，可以直接当 ease 传给 Tween
    // y1、y2 超出 0~1 就会过冲。例：Bezier(0.34f, 1.56f, 0.64f, 1f) 冲过头再回弹
    public static Func<float, float> Bezier(float x1, float y1, float x2, float y2)
    {
        return t =>
        {
            // 已知时间进度 t，用二分法反推贝塞尔参数 u（x 随 u 单调增加，所以一定能夹逼到）
            float lo = 0f, hi = 1f, u = t;
            for (int i = 0; i < 20; i++)
            {
                u = (lo + hi) * 0.5f;
                if (Axis(u, x1, x2) < t) lo = u; else hi = u;
            }
            return Axis(u, y1, y2);   // 用同一个 u 算出动画进度
        };
    }

    // 三次贝塞尔在一个轴上的值（起点 0、终点 1，中间两个控制点 p1、p2）
    static float Axis(float u, float p1, float p2)
    {
        float v = 1f - u;
        return 3f * v * v * u * p1 + 3f * v * u * u * p2 + u * u * u;
    }
}

