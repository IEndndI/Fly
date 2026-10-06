using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// 结局（分镜第 9 幕）：网格归位、「飞」、蜡笔小点落地、最后那句手写、六页片尾、合上——
// 这一段在分镜里是画在 canvas 上的 46 秒动画，这里直接播从分镜录下来的视频：Resources/Video/ending_a、ending_b（VP8 webm，导入成 VideoClip）。
// 表格 flow 列写 end:a / end:b（只写 end 就是不播结局直接回标题）。播放时点击不跳过；音乐换成 bgm_ending。
// 界面零件在 Awake 里自己生成：挂到 Canvas 下最上层的一个空物体上即可。
public class EndingPlayer : MonoBehaviour
{
    public string folder = "Video";                  // Resources 下的文件夹
    public string bgm = "Fly/bgm_ending:vol(0.35)";            // 表格 BGM 列的写法
    public float fade = 0.8f;

    VideoPlayer video; RawImage screen; CanvasGroup group; RenderTexture rt;

    void Awake()
    {
        var r = (RectTransform)transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        screen = GetComponent<RawImage>(); if (screen == null) screen = gameObject.AddComponent<RawImage>();
        screen.color = Color.white; screen.raycastTarget = false;
        group = GetComponent<CanvasGroup>(); if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f; group.blocksRaycasts = false;
        rt = new RenderTexture(1920, 1080, 0);
        screen.texture = rt;
        video = GetComponent<VideoPlayer>(); if (video == null) video = gameObject.AddComponent<VideoPlayer>();
        video.playOnAwake = false; video.isLooping = false;
        video.renderMode = VideoRenderMode.RenderTexture; video.targetTexture = rt;
        video.audioOutputMode = VideoAudioOutputMode.None;       // 视频本身没有声音，音乐走 AudioPlayer
        video.source = VideoSource.VideoClip;
    }

    public IEnumerator Play(string branch, AudioPlayer audio, System.Func<string, Command> load)
    {
        string path = folder + "/ending_" + branch;
        VideoClip clip = Resources.Load<VideoClip>(path);
        if (clip == null) { Debug.LogWarning("找不到结尾视频：Resources/" + path); yield break; }
        video.clip = clip;
        video.Prepare();
        float t0 = Time.time;
        while (!video.isPrepared)
        {
            if (Time.time - t0 > 5f) { Debug.LogWarning("结尾视频没准备好：" + path); yield break; }
            yield return null;
        }
        if (audio != null && load != null) audio.PlayBGM(load(bgm));
        video.Play();
        DialogueManager.blockUntil = float.MaxValue;              // 结尾整段锁住
        for (float t = 0f; t < fade; t += Time.deltaTime) { group.alpha = t / fade; yield return null; }
        group.alpha = 1f;
        // 等它播到最后一帧（isPlaying 在缓冲时会短暂变 false，不能拿它判断结束）
        bool done = false;
        VideoPlayer.EventHandler onEnd = vp => done = true;
        video.loopPointReached += onEnd;
        for (t0 = Time.time; !done && Time.time - t0 < (float)video.clip.length + 3f;) yield return null;
        video.loopPointReached -= onEnd;
        yield return new WaitForSeconds(0.4f);
        DialogueManager.blockUntil = 0f;
    }
}
