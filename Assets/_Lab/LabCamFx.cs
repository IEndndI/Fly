using UnityEngine;
using Unity.Cinemachine;

// 第 5 课：用 Cinemachine 做镜头效果。跟随、瞄准、机位切换交给 Cinemachine，这里只做"根据飞行状态拧镜头参数"。
// C 键：手动切换 追尾 / 侧面 机位
public class LabCamFx : MonoBehaviour
{
    public Rigidbody target;
    public CinemachineCamera chase;         // 追尾机位
    public CinemachineCamera side;          // 侧面机位
    public CinemachineCamera front;         // 正面机位（第二次飞行）
    public bool forceFront;                 // FlightDirector 设：一直用正面

    [Header("速度 → 视野（越快越广，有冲出去的感觉）")]
    public float fovSlow = 55f;
    public float fovFast = 75f;
    public float fastSpeed = 30f;           // 达到这个速度（米/秒）时视野拉满

    [Header("侧倾 → 镜头跟着倾斜多少（0 = 地平线永远水平）")]
    [Range(0f, 1f)] public float rollAmount = 0.3f;

    [Header("速度 → 抖动（风吹的感觉）")]
    public float shakeMax = 0.8f;

    [Header("机位切换")]
    public bool autoSide = true;            // 贴近地面（起飞、降落）时自动切到侧面
    public float sideBelowHeight = 3f;

    CinemachineBasicMultiChannelPerlin noise;
    bool manualSide;

    void Start() { noise = chase.GetComponent<CinemachineBasicMultiChannelPerlin>(); }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C)) manualSide = !manualSide;

        float k = Mathf.Clamp01(target.velocity.magnitude / fastSpeed);   // 0 = 静止，1 = 很快

        // Lens 是个 struct（值类型）：要先整个拿出来、改完再整个放回去
        var lens = chase.Lens;
        lens.FieldOfView = Mathf.Lerp(fovSlow, fovFast, k);
        lens.Dutch = Mathf.DeltaAngle(0f, target.rotation.eulerAngles.z) * rollAmount;
        chase.Lens = lens;

        if (noise != null) noise.AmplitudeGain = shakeMax * k * k;        // 平方：慢的时候几乎不抖，快了才明显

        // 优先级高的机位上场，Brain 负责在两个机位之间平滑过渡
        bool useSide = manualSide || (autoSide && target.position.y < sideBelowHeight);
        side.Priority = useSide ? 20 : 0;
        chase.Priority = 10;
        if (front != null) front.Priority = forceFront ? 30 : 0;
    }
}
