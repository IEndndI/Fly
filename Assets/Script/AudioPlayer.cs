using System.Collections;          // IEnumerator（淡入淡出协程要用）
using System.Collections.Generic;  // Dictionary、List、HashSet
using UnityEngine;                 // Debug、AudioSource、MonoBehaviour……

public class AudioPlayer : MonoBehaviour
{
    public AudioSource bgmSource;
    public AudioSource seSource;
    public float fadeOutTime = 1f;
    public float fadeInTime = 1f;
    public float gapTime = 0f;

    Coroutine currentFade;   // BGM 只有一首，一个遥控器就够
    private Dictionary<string, AudioSource> ambients = new Dictionary<string, AudioSource>();
    // 环境音可以同时响好几个，每台录音机一个遥控器；键用 AudioSource 不用名字，
    // 因为 stop 后马上重开同名环境音时，旧的还在淡出，两台同名
    private Dictionary<AudioSource, Coroutine> ambientFades = new Dictionary<AudioSource, Coroutine>();

    // ───────── 环境音 ─────────

    public void PlayAmbient(Command cmd)
    {
        float inTime = GetArg(cmd, "in", fadeInTime);
        float vol    = GetArg(cmd, "vol", 1f);

        // 已经在响：不重开，只把音量淡到 vol（跟 BGM 同名分支一个思路）
        if (ambients.TryGetValue(cmd.name, out AudioSource playing))
        {
            FadeAmbient(playing, FadeVolume(playing, vol, inTime));
            return;
        }

        AudioClip clip = LoadClip(cmd.name, "环境音");
        if (clip == null) return;

        AudioSource source = gameObject.AddComponent<AudioSource>();   // 运行时现装一台录音机
        source.clip = clip;
        source.loop = true;
        source.volume = 0f;            // 先拧到 0：FadeVolume 从当前音量出发，新录音机默认是 1
        source.Play();
        ambients[cmd.name] = source;
        FadeAmbient(source, FadeVolume(source, vol, inTime));
    }

    public void StopAmbient(Command cmd)
    {
        float outTime = GetArg(cmd, "out", fadeOutTime);
        if (!ambients.TryGetValue(cmd.name, out AudioSource source))
        {
            Debug.LogWarning("环境音 " + cmd.name + " 没在响，无法停止");
            return;
        }
        ambients.Remove(cmd.name);     // 马上从花名册划掉，同名的可以立刻重开
        FadeAmbient(source, FadeOutAndDestroy(source, outTime));
    }

    IEnumerator FadeOutAndDestroy(AudioSource source, float time)
    {
        yield return FadeVolume(source, 0f, time);   // 等淡出完再拆，否则声音戛然而止
        ambientFades.Remove(source);
        Destroy(source);
    }

    // 先停这台录音机的旧遥控器，再开新的
    void FadeAmbient(AudioSource source, IEnumerator fade)
    {
        if (ambientFades.TryGetValue(source, out Coroutine old)) StopCoroutine(old);
        ambientFades[source] = StartCoroutine(fade);
    }

    // ───────── BGM ─────────

    public void PlayBGM(Command cmd)
    {
        float outTime = GetArg(cmd, "out", fadeOutTime);
        float inTime  = GetArg(cmd, "in",  fadeInTime);
        float gap     = GetArg(cmd, "gap", gapTime);
        float vol     = GetArg(cmd, "vol", 1f);

        if (cmd.name == "{clear}")
        {
            if (currentFade != null) StopCoroutine(currentFade);
            bgmSource.Stop();
            return;
        }

        AudioClip clip = LoadClip(cmd.name, "音乐");
        if (clip == null) return;

        // 同一首正在放：不重放；写了 vol 就只把音量淡过去
        if (bgmSource.clip == clip && bgmSource.isPlaying)
        {
            if (cmd.namedArgs.ContainsKey("vol"))
            {
                if (currentFade != null) StopCoroutine(currentFade);
                currentFade = StartCoroutine(FadeVolume(bgmSource, vol, inTime));
            }
            return;
        }

        if (currentFade != null) StopCoroutine(currentFade);   // 之前的淡变还没完，先按停止
        currentFade = StartCoroutine(SwitchBGM(clip, outTime, inTime, gap, vol));
    }

    IEnumerator SwitchBGM(AudioClip newClip, float outTime, float inTime, float gap, float vol)
    {
        if (bgmSource.isPlaying)
            yield return FadeVolume(bgmSource, 0f, outTime);   // 旧歌淡出

        yield return new WaitForSeconds(gap);

        bgmSource.clip = newClip;
        bgmSource.volume = 0f;         // 先拧到 0，淡入才是从无到有
        bgmSource.Play();

        yield return FadeVolume(bgmSource, vol, inTime);       // 新歌淡入
    }

    // ───────── 音效 ─────────

    public void PlaySE(Command cmd)
    {
        AudioClip clip = LoadClip(cmd.arg, "音效");   // se:whoosh 的名字存在 arg 里
        if (clip == null) return;
        seSource.PlayOneShot(clip, GetArg(cmd, "vol", 1f));   // 叠着放、不打断上一个
    }

    public float GetSELength(string seName)
    {
        AudioClip clip = LoadClip(seName, "音效");
        return clip == null ? 0f : clip.length;
    }

    // ───────── 通用 ─────────

    // 从当前音量淡到 to；BGM 和环境音共用
    IEnumerator FadeVolume(AudioSource source, float to, float time)
    {
        float from = source.volume;
        float elapsed = 0f;
        while (elapsed < time)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(from, to, elapsed / time);
            yield return null;
        }
        source.volume = to;            // time 为 0 时循环一次都不进，靠这句落到目标值
    }

    AudioClip LoadClip(string clipName, string kind)
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/" + clipName);
        if (clip == null) Debug.LogWarning("找不到" + kind + "：" + clipName);
        return clip;
    }

    // 没写这个参数 → 默认值；写了但不是数字 → 警告 + 默认值
    float GetArg(Command cmd, string key, float defaultValue)
    {
        if (cmd.namedArgs.TryGetValue(key, out string text))
        {
            if (float.TryParse(text, out float value))
                return value;
            Debug.LogWarning("无法转换为数字：" + key + "，使用默认值：" + defaultValue);
        }
        return defaultValue;
    }
}
