using UnityEngine;

// 纸片立绘的动作（CharacterStage 自动挂上）：
// 登场 = 像把一张纸片按到书页上：稍微歪着、小一点、透明 → 落定（轻轻回弹一下）
// 换表情 = 纸片轻轻一跳
// 待着 = 像立在风里的纸，慢慢地左右晃（脚底是轴心）
// 退场 = 歪一下、淡掉，然后销毁
[RequireComponent(typeof(CanvasGroup))]
public class PaperSprite : MonoBehaviour
{
    public float enterTime = 0.4f;
    public float swayAngle = 0.6f;          // 晃动幅度（度）
    public float swayPeriod = 4.5f;         // 晃一个来回几秒

    RectTransform rt; CanvasGroup group;
    float t = 1f, dur = 1f;                 // 当前这段动作进行到哪
    float tilt0, scale0 = 1f, alpha0 = 1f;  // 这段动作的起点
    bool leaving;
    float phase;

    void Awake()
    {
        rt = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        phase = Random.value * 10f;         // 每张纸晃的节奏错开
    }

    public void Enter()
    {
        Begin(enterTime, Random.Range(-4f, 4f), 0.9f, 0f);
        group.alpha = 0f;
    }

    public void Swap() { if (!leaving) Begin(0.22f, Random.Range(-1.5f, 1.5f), 0.97f, group.alpha); }

    public void Leave()
    {
        leaving = true;
        Begin(0.25f, 0f, 1f, group.alpha);
        tilt0 = Random.value < 0.5f ? -3f : 3f;   // 退场往哪边歪（终点）
    }

    void Begin(float d, float tilt, float scale, float alpha)
    {
        dur = d; t = 0f; tilt0 = tilt; scale0 = scale; alpha0 = alpha;
    }

    void Update()
    {
        t = Mathf.Min(1f, t + Time.deltaTime / dur);
        float sway = Mathf.Sin((Time.time + phase) * Mathf.PI * 2f / swayPeriod) * swayAngle;

        if (leaving)
        {
            float e = t * t;                                              // 慢慢开始、越走越快
            group.alpha = alpha0 * (1f - e);
            rt.localRotation = Quaternion.Euler(0f, 0f, sway + tilt0 * e);
            rt.localScale = Vector3.one * (1f - 0.04f * e);
            if (t >= 1f) Destroy(gameObject);
            return;
        }

        float o = 1f + 2.2f * Mathf.Pow(t - 1f, 3f) + 1.2f * Mathf.Pow(t - 1f, 2f);   // ease-out-back：冲过头一点再回来
        group.alpha = Mathf.Lerp(alpha0, 1f, Mathf.Clamp01(t * 1.8f));
        rt.localRotation = Quaternion.Euler(0f, 0f, sway + Mathf.LerpUnclamped(tilt0, 0f, o));
        rt.localScale = Vector3.one * Mathf.LerpUnclamped(scale0, 1f, o);
    }
}
