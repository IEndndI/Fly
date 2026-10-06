using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

// 长按快进：按住鼠标左键 / 空格 / 手指，视频 4 倍速；松开恢复。右下角显示一个小小的「▶▶ 4×」。
// 挂在带 VideoPlayer 的物体上（开场、结局都用它）；Enabled = false 时不响应。
public class HoldToSpeed : MonoBehaviour
{
    public float fast = 4f;
    public bool Enabled = true;
    public bool Holding { get; private set; }

    VideoPlayer video; TextMeshProUGUI badge;
    static readonly Color Cobalt = new Color(0f, 0.278f, 0.671f);

    void Awake()
    {
        video = GetComponent<VideoPlayer>();
        var go = new GameObject("FastBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-120f, 40f); rt.sizeDelta = new Vector2(200f, 20f);
        badge = go.GetComponent<TextMeshProUGUI>();
        badge.text = ">>  4×"; badge.fontSize = 14f; badge.characterSpacing = 8f; badge.alignment = TextAlignmentOptions.BottomRight;
        badge.color = Cobalt; badge.alpha = 0f; badge.raycastTarget = false;
        var m = badge.fontMaterial; m.EnableKeyword("UNDERLAY_ON");
        m.SetColor("_UnderlayColor", new Color(0.949f, 0.925f, 0.875f, 0.9f)); m.SetFloat("_UnderlayDilate", 0.6f); m.SetFloat("_UnderlaySoftness", 0.6f);
    }

    void Update()
    {
        Holding = Enabled && video != null && video.isPlaying &&
                  (Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space) || Input.touchCount > 0);
        if (video != null && video.isPlaying)
        {
            video.playbackSpeed = Holding ? fast : 1f;
        }
        badge.alpha = Mathf.MoveTowards(badge.alpha, Holding ? 1f : 0f, Time.unscaledDeltaTime / 0.15f);
    }
}
