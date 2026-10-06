using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// 第二章：起跑 → 离地 → 2.5D 的田野 → 穿云 → 深空 → 回首地球 → 下降，一镜到底。
// 画面是从分镜按「飞行时钟」逐帧渲染的视频（Resources/Video/flight，视频的第 t 秒 = 时钟 t 秒）。
// 表格每一句飞行台词写 flight:seg(a,b)：这句拥有时钟 a→b 这一段。时钟和分镜一样——
// 不会停下来等字，而是按这句需要的阅读时间把速度放慢（最慢 0.3 倍），刚好在读完时走到 b；落后了就加速追。
// 回到坡脚写 flight:off（淡出，露出黄昏的草坡）。界面零件在 Awake 里自己生成：挂在 Canvas 下背景层之上、对话框之下。
public class FlightVideo : MonoBehaviour
{
    public static FlightVideo Instance { get; private set; }
    public string clipPath = "Video/flight";

    public bool Active => on;
    public bool AtSegmentEnd => !on || fc >= segB - 0.02f || lineClock > (segB - segA) / 0.3f + 2f;   // 这句的画面走完了没有（锁住时 DialogueManager 等它）；保险：最多等这么久
    public bool Dark => on && fc >= 41f;            // 深空以后画面是暗的（分镜同样从 41 秒起换纸色字）
    // 长按 = 1.5 倍速：画面、打字、阅读等待一起快；音乐音效不变（DialogueManager 读 Speed）
    public static float Speed => Instance != null && Instance.holding ? 1.5f : 1f;
    bool holding; TMPro.TextMeshProUGUI badge; float lineClock;   // 这句的画面走完了没有（锁住时 DialogueManager 等它）；保险：最多等这么久

    VideoPlayer video; RawImage screen; CanvasGroup group; RenderTexture rt;
    DialogueManager dialogue; AutoLock autoLock;
    bool on, prepared;
    float fc, segA, segB, rate = 1f, lineT0, need;

    void Awake()
    {
        Instance = this;
        var r = (RectTransform)transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        screen = gameObject.AddComponent<RawImage>(); screen.raycastTarget = false;
        group = gameObject.AddComponent<CanvasGroup>(); group.alpha = 0f; group.blocksRaycasts = false;
        rt = new RenderTexture(1920, 1080, 0); screen.texture = rt;
        video = gameObject.AddComponent<VideoPlayer>();
        video.playOnAwake = false; video.isLooping = false; video.renderMode = VideoRenderMode.RenderTexture; video.targetTexture = rt;
        video.audioOutputMode = VideoAudioOutputMode.None; video.source = VideoSource.VideoClip;
        video.clip = Resources.Load<VideoClip>(clipPath);
        if (video.clip == null) Debug.LogWarning("找不到飞行视频：Resources/" + clipPath);
        else { video.prepareCompleted += _ => prepared = true; video.Prepare(); }   // 先准备好，第一句就不会黑一下
        dialogue = FindObjectOfType<DialogueManager>(); autoLock = FindObjectOfType<AutoLock>();
        var bg = new GameObject("FastBadge", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        var br = bg.GetComponent<RectTransform>(); br.SetParent(transform.parent, false);
        br.anchorMin = br.anchorMax = new Vector2(0f, 1f); br.pivot = new Vector2(0f, 1f); br.anchoredPosition = new Vector2(120f, -550f); br.sizeDelta = new Vector2(240f, 20f);
        badge = bg.GetComponent<TMPro.TextMeshProUGUI>(); badge.text = ">>  1.5×"; badge.fontSize = 14f; badge.characterSpacing = 8f; badge.raycastTarget = false; badge.alpha = 0f;
    }

    // flight:seg(a,b) 或 flight:off
    public void Apply(Command cmd)
    {
        if (cmd.arg == "off") { on = false; return; }
        if (!cmd.namedArgs.TryGetValue("seg", out string s)) { Debug.LogWarning("flight 少了 seg(a,b)"); return; }
        var p = s.Split(',');
        if (p.Length < 2 || !float.TryParse(p[0], out float a) || !float.TryParse(p[1], out float b)) { Debug.LogWarning("flight seg 写错了：" + s); return; }
        if (!on) { fc = a; rate = 1f; Seek(a); }          // 从别处进来（第一句、读档）：直接从这段开头开始，不从 0 追
        if (video.clip != null) b = Mathf.Min(b, (float)video.clip.length - 0.1f);   // 不能走到视频最末：VideoPlayer 播完会自己跳回第 0 帧
        on = true; segA = a; segB = b; lineT0 = Time.time; lineClock = 0f;
        string line = DialogueManager.currentLine ?? "";
        need = line.Length * SaveSystem.TextDelay + (autoLock != null ? autoLock.ReadTime(line) : 1f + 0.07f * line.Length);
    }

    void Seek(float t) { if (video.clip != null) { video.time = t; } }

    void Update()
    {
        group.alpha = Mathf.MoveTowards(group.alpha, on && prepared ? 1f : 0f, Time.deltaTime / (on ? 0.35f : 0.6f));
        if (!on || video.clip == null) { if (video.isPlaying && group.alpha <= 0f) video.Pause(); return; }
        bool locked = autoLock != null && autoLock.IsLocked;
        holding = locked && (Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space) || Input.touchCount > 0);
        float dt = Mathf.Min(0.25f, Time.deltaTime) * Speed;       // 长按：时钟整体 1.5 倍
        lineClock += dt;
        Color bc = Color.Lerp(new Color(0f, 0.278f, 0.671f), new Color(0.949f, 0.925f, 0.875f), Dark ? 1f : 0f);
        badge.color = new Color(bc.r, bc.g, bc.b, Mathf.MoveTowards(badge.alpha, holding ? 1f : 0f, Time.deltaTime / 0.15f));
        // 和分镜 flightClock 一样：锁住时按阅读时间放慢；落后于这段开头就加速追
        float target = 1f;
        if (locked)
        {
            float rc = Mathf.Max(0f, segB - Mathf.Max(fc, segA)), rtime = Mathf.Max(0.05f, need - lineClock);
            target = rc <= 0f ? 1f : Mathf.Clamp(rc / rtime, 0.3f, 1f);
        }
        rate += (target - rate) * (1f - Mathf.Exp(-dt * 2.2f));
        float lag = segA - fc, sp = lag > 0f ? 1f + Mathf.Min(2.5f, lag * 0.5f) : rate;
        if (fc < segB) fc = Mathf.Min(segB, fc + dt * sp);              // 时钟自己数（不读 video.time：暂停/转码后它可能读回 0）

        // 视频跟着时钟：用倍速跟；差太多（>1.5 秒）才重新定位，不来回跳
        bool moving = fc < segB - 0.02f;
        if (moving)
        {
            if (!video.isPlaying) { Seek(fc); video.Play(); }
            video.playbackSpeed = Mathf.Clamp(sp * Speed, 0.1f, 4f);
            if (video.isPrepared && Mathf.Abs((float)video.time - fc) > 1.5f) Seek(fc);
        }
        else if (video.isPlaying) video.Pause();
    }
}
