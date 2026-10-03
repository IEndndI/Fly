using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EffectPlayer : MonoBehaviour
{
    public Image flashOverlay;

    public float shakeDuration = 0.4f;
    public float flashDuration = 0.3f;

    public float jumpDuration = 0.3f;
    public float jumpHeight = 30f;
    public TimelinePlayer timelinePlayer;

    public AudioPlayer audioPlayer;

    public IEnumerator Shake(float duration, RectTransform target)
    {
        
        if (target == null) yield break;
        if (duration < 0) 
            {
                duration = shakeDuration;
            }

        
        Vector3 originalPos = target.anchoredPosition3D;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            target.anchoredPosition3D = originalPos + new Vector3(Random.Range(-10f, 10f), 0, 0);
            yield return null;
        }
        target.anchoredPosition3D = originalPos;
    }

    public IEnumerator Flash(float duration)
    {
        if (flashOverlay == null) yield break;
        if (duration < 0)
            {
                duration = flashDuration;
            }
        

        float half = duration / 2f;
        float elapsed = 0f;

        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            Color c = flashOverlay.color; c.a = Mathf.Lerp(0f, 1f, elapsed / half);
            flashOverlay.color = c;
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            Color c = flashOverlay.color; c.a = Mathf.Lerp(1f, 0f, elapsed / half);
            flashOverlay.color = c;
            yield return null;
        }
    }

    public IEnumerator Jump(float duration, RectTransform target)
    {
        if (target == null) yield break;
        if (duration < 0 )
            {
                duration = jumpDuration;
            }
        
        Vector3 originalPos = target.anchoredPosition3D;
        float elapsed = 0f;
        

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            target.anchoredPosition3D = originalPos + new Vector3(0, Mathf.Sin(progress * Mathf.PI) * jumpHeight, 0);
            yield return null;
        }
        
        target.anchoredPosition3D = originalPos;
    }

    public void Play(Command cmd, RectTransform target)
    {
        if (cmd.name == "shake") StartCoroutine(Shake(cmd.duration, target));
        else if (cmd.name == "flash") StartCoroutine(Flash(cmd.duration));
        else if (cmd.name == "jump") StartCoroutine(Jump(cmd.duration, target));
        else if (cmd.name == "play") timelinePlayer.Play(cmd.arg);
        else if (cmd.name == "se") audioPlayer.PlaySE(cmd);
        else Debug.LogWarning("未知效果：" + cmd.name);
    }

    public float GetDuration(Command cmd)
    {
        if (cmd.duration >= 0) return cmd.duration;

        if (cmd.name == "shake") return shakeDuration;
        else if (cmd.name == "flash") return flashDuration;
        else if (cmd.name == "jump") return jumpDuration;
        else if (cmd.name == "play") return timelinePlayer.GetDuration(cmd.arg);
        else if (cmd.name == "se")
        {
            if (cmd.flags.Contains("full")) return audioPlayer.GetSELength(cmd.arg);
            return 0f;
        }
        else return 0f;
    }
}