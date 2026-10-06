using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// 开场（分镜 0:00–0:20.5：校样 → 笔记本 → 起飞 → 「飞」落定）：播从分镜录下来的视频（Resources/Video/opening，自带声音）。
// 只在启动游戏后第一次进标题时播；从游戏「合上书」、从结局回来都直接是标题。长按 = 4 倍速（HoldToSpeed）。
// 播完停在最后一帧，交给 TitleScreen.Entrance()：标题从这一帧上铺出来。挂在标题场景 Canvas 下最上层的空物体上。
public class OpeningPlayer : MonoBehaviour
{
    public static bool Played;                     // 这次启动已经播过了
    public string clipPath = "Video/opening";

    VideoPlayer video; RenderTexture rt; TitleScreen title; bool ended; AudioSource se; float lastCue = -1f;

    // 分镜开场的音效时间点（index.html 的 CUES；glitch 默认关着，不放）
    struct Cue { public float t; public string n; public Cue(float t, string n) { this.t = t; this.n = n; } }
    static readonly Cue[] Cues = {
        new Cue(0.3f, "reg"), new Cue(0.55f, "reg"), new Cue(3.2f, "bleed"), new Cue(4.4f, "fold"), new Cue(4.6f, "paper"),
        new Cue(5.4f, "tick"), new Cue(5.7f, "tick"), new Cue(6f, "tick"), new Cue(8.2f, "scratch7"), new Cue(10.45f, "rub"),
        new Cue(16f, "yard0"), new Cue(16.15f, "yard1"), new Cue(16.3f, "yard2"), new Cue(16.45f, "yard3"), new Cue(16.6f, "yard4"),
        new Cue(16.75f, "yard5"), new Cue(16.9f, "yard6"), new Cue(17.05f, "yard7"), new Cue(17.2f, "yard8"),
        new Cue(17.4f, "whoosh"), new Cue(17.56f, "whoosh"), new Cue(17.72f, "whoosh"),
        new Cue(18.83f, "flick"), new Cue(18.92f, "flick"), new Cue(19.08f, "flick"), new Cue(19.25f, "flick"), new Cue(19.3f, "fly"),
        new Cue(20.45f, "land"),
    };
    void Sfx(string n) { var c = Resources.Load<AudioClip>("Audio/Fly/sfx_" + n); if (c != null) se.PlayOneShot(c); }

    void Awake()
    {
        title = FindObjectOfType<TitleScreen>();
        var clip = Resources.Load<VideoClip>(clipPath);
        if (Played || TitleScreen.CoveredEntry || TitleScreen.FromBlack || clip == null || title == null)
        {
            if (clip == null) Debug.LogWarning("找不到开场视频：Resources/" + clipPath);
            Destroy(gameObject); return;
        }
        Played = true;
        var r = (RectTransform)transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        r.SetAsLastSibling();
        var screen = gameObject.AddComponent<RawImage>(); screen.raycastTarget = true;      // 播放时挡住标题的点击
        rt = new RenderTexture(1920, 1080, 0); screen.texture = rt;
        video = gameObject.AddComponent<VideoPlayer>();
        video.playOnAwake = false; video.isLooping = false; video.clip = clip;
        video.renderMode = VideoRenderMode.RenderTexture; video.targetTexture = rt;
        video.audioOutputMode = VideoAudioOutputMode.None;                                     // 视频全程不出声：音乐（标题曲）从开头就响，音效按分镜的时间点另外放
        se = gameObject.AddComponent<AudioSource>(); se.playOnAwake = false; se.volume = SaveSystem.Sfx;
        gameObject.AddComponent<HoldToSpeed>();
        video.loopPointReached += _ => End();
        screen.color = new Color(1f, 1f, 1f, 0f);                                          // 第一帧解出来之前不显示（免得闪一下黑）
        video.Prepare();
    }

    IEnumerator Begin()
    {
        while (AIDisclosure.Active) yield return null;                                     // 先看完创作声明
        while (!video.isPrepared) yield return null;
        video.Play();
        title.StartMusic(3f);                                                              // 标题音乐从视频一开始就淡入
        var screen = GetComponent<RawImage>();
        while (video.frame < 1) yield return null;
        screen.color = Color.white;
    }

    void Start() { if (video != null) { title.PrepareForOpening(); StartCoroutine(Begin()); } }    // TitleScreen 的 Awake 已经跑完：把标题先藏起来

    void Update()
    {
        // 快到最后一帧就停住（不让 VideoPlayer 播完跳回第 0 帧），把画面交给标题
        if (video != null && video.isPlaying)
        {
            float now = (float)video.time;
            foreach (var c in Cues) if (c.t > lastCue && c.t <= now) Sfx(c.n);              // 分镜 CUES：按视频的时钟放（快进时跟着变快）
            lastCue = now;
        }
        if (!ended && video != null && video.isPlaying && video.time >= video.clip.length - 0.06) End();
    }

    void End()
    {
        if (ended) return;
        ended = true;
        video.Pause(); Sfx("drop");
        GetComponent<HoldToSpeed>().Enabled = false;
        GetComponent<RawImage>().raycastTarget = false;
        transform.SetSiblingIndex(title.transform.GetSiblingIndex());   // 垫到标题底下：标题从这一帧上铺出来
        StartCoroutine(title.Entrance(() => Destroy(gameObject)));
    }

    void OnDestroy() { if (rt != null) rt.Release(); }
}
