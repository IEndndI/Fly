using System.Collections;
using UnityEngine;

public class FadeEffect : MonoBehaviour
{
    public CanvasGroup canvasGroup; // 要控制透明度的对象
    public float fadeDuration = 1f; // 淡入/淡出总共持续多少秒

    void Start()
    {
        // 游戏一开始，先做一次淡入效果（从透明变为不透明）
        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        canvasGroup.alpha = 0f; // 一开始完全透明
        float timeElapsed = 0f; // 记录已经过去多少秒

        while (timeElapsed < fadeDuration)
        {
            timeElapsed += Time.deltaTime; // 每一帧累加"上一帧到这一帧经过的时间"
            canvasGroup.alpha = timeElapsed / fadeDuration; // 算出当前应该的透明度（0到1之间）
            yield return null; // 暂停一帧，下一帧再继续
        }

        canvasGroup.alpha = 1f; // 确保最后精确停在完全不透明
    }

    IEnumerator FadeOut()
    {
        canvasGroup.alpha = 1f;
        float timeElapsed = 0f;

        while (timeElapsed < fadeDuration)
        {
            timeElapsed += Time.deltaTime;
            canvasGroup.alpha = 1f - (timeElapsed / fadeDuration); // 这次是从1减到0
            yield return null;
        }

        canvasGroup.alpha = 0f;
    }
}