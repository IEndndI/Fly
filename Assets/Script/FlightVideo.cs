using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// 第二章：起跑 → 离地 → 2.5D 的田野 → 穿云 → 深空 → 回首地球 → 下降，一镜到底。
// 画面是从分镜按「飞行时钟」逐帧渲染的视频（Resources/Video/flight，视频的第 t 秒 = 时钟 t 秒）。
// 表格每一句飞行台词写 flight:seg(a,b)：这句拥有时钟 a→b 这一段。
// v1.1：画面永远按 1 倍速匀速走（长按 1.5 倍），不再放慢、不再停下来等字——是「文字追画面」：
// 这句的打字速度会自动加快到在这段的前 55% 打完，锁住时画面走到 b 就翻下一句。
// 回到坡脚写 flight:off（淡出，露出黄昏的草坡）。界面零件在 Awake 里自己生成：挂在 Canvas 下背景层之上、对话框之下。
public class FlightVideo : MonoBehaviour
{
    public static FlightVideo Instance { get; private set; }
    public string clipPath = "Video/flight";

    public bool Active => on;
    public bool AtSegmentEnd => !on || fc >= segB - 0.02f || lineClock > (segB - segA) + 3f;   // 这句的画面走完了没有（锁住时 DialogueManager 等它）；保险：最多等这么久
    public bool Dark => on && fc >= 41f;            // 深空以后画面是暗的（分镜同样从 41 秒起换纸色字）
    // 长按 = 1.5 倍速：画面、打字、阅读等待一起快；音乐音效不变（DialogueManager 读 Speed）
    public static float Speed => Instance != null && Instance.holding ? 1.5f : 1f;
    bool holding; TMPro.TextMeshProUGUI badge; float lineClock;   // 这句的画面走完了没有（锁住时 DialogueManager 等它）；保险：最多等这么久

    VideoPlayer video; RawImage screen; CanvasGroup group; RenderTexture rt;
    DialogueManager dialogue; AutoLock autoLock;
    bool on, prepared;
    // 定位：播放中直接改 video.time 会让 VideoPlayer 卡死在一帧（快进时每句都跳，必现）。
    // 所以定位一律走「停下 → 重新准备 → 准备好时设时间 → 再播」，和从某一句直接进来那条路一样稳
    float pendingSeek = -1f, stuckT; double lastVT = -1;
    float fc, segA, segB, lineT0, charDelay = 0.04f, stall;
    bool hasFrame;                                   // 视频真的出过画面了（之前 RenderTexture 是白的，不能显示）

    // 打字每个字等多久（DialogueManager.TypeSegment 读）：飞行中 = 不慢于「这段的 55% 时间打完」；长按再快 1.5 倍
    public static float CharDelay(float baseDelay)
    {
        var f = Instance;
        float d = f != null && f.on ? Mathf.Min(baseDelay, f.charDelay) : baseDelay;
        return d / Speed;
    }

    void Awake()
    {
        Instance = this;
        var r = (RectTransform)transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        screen = gameObject.AddComponent<RawImage>(); screen.raycastTarget = false;
        gameObject.AddComponent<Bleed>(); gameObject.AddComponent<CoverUV>();             // v1.1：铺到屏幕边，按比例裁切，不拉伸
        group = gameObject.AddComponent<CanvasGroup>(); group.alpha = 0f; group.blocksRaycasts = false;
        rt = new RenderTexture(1920, 1080, 0); screen.texture = rt;
        video = gameObject.AddComponent<VideoPlayer>();
        video.playOnAwake = false; video.isLooping = false; video.renderMode = VideoRenderMode.RenderTexture; video.targetTexture = rt;
        video.audioOutputMode = VideoAudioOutputMode.None; video.source = VideoSource.VideoClip;
        video.clip = Resources.Load<VideoClip>(clipPath);
        if (video.clip == null) Debug.LogWarning("找不到飞行视频：Resources/" + clipPath);
        else
        {
            video.prepareCompleted += _ =>
            {
                prepared = true;
                if (pendingSeek >= 0f) { video.time = pendingSeek; pendingSeek = -1f; }   // 准备好、还没播：这时设时间最稳
            };
            video.errorReceived += (_, msg) => Debug.LogWarning("飞行视频出错：" + msg);
            video.Prepare();                                                     // 先准备好，第一句就不会黑一下
        }
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
        if (!on || Mathf.Abs(fc - a) > 1.5f) { fc = a; Seek(a); }   // 从别处进来（第一句、读档、跳转）：直接从这段开头开始
        if (video.clip != null) b = Mathf.Min(b, (float)video.clip.length - 0.1f);   // 不能走到视频最末：VideoPlayer 播完会自己跳回第 0 帧
        on = true; segA = a; segB = b; lineT0 = Time.time; lineClock = 0f;
        string line = (DialogueManager.currentLine ?? "").Replace("{w}", "");
        float budget = Mathf.Max(0.4f, (segB - Mathf.Max(fc, segA)) * 0.55f);
        charDelay = line.Length > 0 ? budget / line.Length : SaveSystem.TextDelay;
    }

    void Seek(float t)
    {
        if (video.clip == null) return;
        pendingSeek = t;
        if (video.isPrepared && !video.isPlaying) { video.time = t; pendingSeek = -1f; return; }   // 还没开播：直接设
        if (!prepared) return;                                               // 正在重新准备：只更新目标，准备好时会用上
        prepared = false; video.Stop(); video.Prepare();                     // 播放中：停下重来
    }

    // 看门狗：该放的时候画面 1.5 秒没动，就重新准备视频、从时钟的位置接着放
    void Watchdog(bool moving)
    {
        if (!moving || !video.isPlaying) { stuckT = 0f; lastVT = video.time; return; }
        if (video.time != lastVT) { stuckT = 0f; lastVT = video.time; return; }
        stuckT += Time.unscaledDeltaTime;
        if (stuckT < 1.5f) return;
        stuckT = 0f; pendingSeek = fc; prepared = false;
        video.Stop(); video.Prepare();
    }

    void Update()
    {
        bool decoding = video.isPlaying && video.frame >= 0 && video.time > 0.01;   // 视频在实打实地出画面
        if (decoding) hasFrame = true;
        group.alpha = Mathf.MoveTowards(group.alpha, on && prepared && hasFrame ? 1f : 0f, Time.deltaTime / (on ? 0.35f : 0.6f));
        if (!on || video.clip == null) { if (video.isPlaying && group.alpha <= 0f) { video.Pause(); hasFrame = false; } return; }
        bool locked = autoLock != null && autoLock.IsLocked;
        holding = locked && (Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space) || Input.touchCount > 0);
        float dt = Mathf.Min(0.25f, Time.deltaTime) * Speed;       // 长按：时钟整体 1.5 倍
        lineClock += dt;
        Color bc = Color.Lerp(new Color(0f, 0.278f, 0.671f), new Color(0.949f, 0.925f, 0.875f), Dark ? 1f : 0f);
        badge.color = new Color(bc.r, bc.g, bc.b, Mathf.MoveTowards(badge.alpha, holding ? 1f : 0f, Time.deltaTime / 0.15f));
        // 画面匀速走：锁住时可以一直走（DialogueManager 到 b 就翻句，下一段接着放）；
        // 没锁（第一句要自己点）时最多比这段多走 1.5 秒，再慢就停在那里等你
        float end = video.clip != null ? (float)video.clip.length - 0.1f : segB;
        float limit = Mathf.Min(end, locked ? end : segB + 1.5f);
        // 时钟自己数，但跟着视频真正放到的地方走：刚定位 / 手机解码慢、还没出画面时先等它（最多 1.5 秒，防止卡死）
        if (fc < limit - 0.01f && !decoding) { stall += Time.deltaTime; if (stall < 1.5f) dt = 0f; } else stall = 0f;
        fc = Mathf.Min(limit, fc + dt);
        if (decoding) { float d = (float)video.time - fc; if (Mathf.Abs(d) < 1f) fc = Mathf.Min(limit, fc + d * Mathf.Min(1f, Time.deltaTime * 4f)); }

        bool moving = fc < limit - 0.01f;
        if (moving)
        {
            if (!video.isPlaying && video.isPrepared) { if (Mathf.Abs((float)video.time - fc) > 0.5f) { video.time = fc; } video.Play(); }
            video.playbackSpeed = Speed;                                  // 只有 1 或 1.5，不再忽快忽慢（安卓上变速会卡）
            if (decoding && Mathf.Abs((float)video.time - fc) > 1.5f) Seek(fc);   // 差太多才重来（重来要零点几秒，不常发生）
        }
        else if (video.isPlaying) video.Pause();
        Watchdog(moving);
    }
}
