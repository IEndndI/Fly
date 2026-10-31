using UnityEngine;

// 第 4 课：飞行模型。四个力：推力、升力、重力、阻力。
// W/S 俯仰　A/D ←→ 侧倾（侧倾就会转弯）　↑↓ 油门　R 复位
[RequireComponent(typeof(Rigidbody), typeof(LabInput))]
public class LabFly : MonoBehaviour
{
    [Header("① 推力")]
    public float maxThrust = 3f;            // 油门全开的推力。质量是 1，所以推力 = 加速度（米/秒²）。调到 2：刚要飞起来又被压下去
    public float throttleRate = 0.5f;       // 按住 ↑↓ 每秒改变多少油门

    [Header("② 升力")]
    public float liftBase = 0.024f;         // 迎角 0° 时的升力系数。0.024 × 20² ≈ 9.8：72 km/h 时升力刚好托住重力
    public float liftPerDegree = 0.002f;    // 迎角每多 1°，升力系数加多少
    public float stallAngle = 15f;          // 失速迎角：从这里开始升力往下掉
    public float stallWidth = 8f;           // 用多少度慢慢掉完。0 = 一下子掉光（像被大手按下去）
    [Range(0f, 1f)] public float stallLift = 0.4f;   // 失速掉完后剩几成升力

    [Header("③ 重力：Rigidbody 自带，改 Project Settings > Physics > Gravity")]

    [Header("④ 阻力")]
    public float drag = 0.005f;             // 阻力 = drag × 速度²
    public float sideGrip = 2f;             // 机身挡住侧滑，不让飞机像在冰上横着漂

    [Header("稳定性")]
    public float noseFollow = 1f;           // 机头自己慢慢转向速度方向（风向标效应）

    [Header("操纵（度/秒）")]
    public float pitchSpeed = 25f;
    public float rollSpeed = 25f;
    public float fullControlSpeed = 15f;    // 低于这个速度，舵面没劲
    public bool invertPitch = false;
    public float inputSmooth = 3f;          // 按键过渡：每秒最多变化多少（3 = 约 0.33 秒推满，松手也是 0.33 秒回零）

    [Header("起点（按 R 生效）")]
    public bool startOnGround = true;
    public float startSpeed = 20f;

    public float Throttle => throttle;      // 给 FlightDirector 看的

    Rigidbody rb;
    LabInput controls;
    float throttle;
    Vector2 input;                          // x = 俯仰，y = 侧倾。Update 里平滑，FixedUpdate 里用
    float aoa, lift, speed;
    bool stalled;
    Vector3 startPos; Quaternion startRot;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        controls = GetComponent<LabInput>();
        rb.mass = 1f; rb.useGravity = true; rb.drag = 0f; rb.angularDrag = 0f;
        rb.freezeRotation = true;                                   // 旋转由我们自己控制，撞到东西也不乱翻
        rb.interpolation = RigidbodyInterpolation.Interpolate;      // 物理 50 次/秒，画面更多帧：插值补上，镜头不抖
        rb.centerOfMass = Vector3.zero;                             // 重心放在机身中心，旋转也绕这里
        var slick = new PhysicMaterial("Slick") { dynamicFriction = 0.05f, staticFriction = 0.05f, frictionCombine = PhysicMaterialCombine.Minimum };
        foreach (var c in GetComponentsInChildren<Collider>()) c.material = slick;   // 起落架当作轮子：地面摩擦很小
        startPos = transform.position; startRot = transform.rotation;
        ResetPlane();
    }

    void ResetPlane()
    {
        Vector3 p = startOnGround ? new Vector3(startPos.x, 0.5f, startPos.z) : startPos;
        rb.position = p; rb.rotation = startRot;
        transform.SetPositionAndRotation(p, startRot);
        rb.velocity = startOnGround ? Vector3.zero : startRot * Vector3.forward * startSpeed;
        throttle = startOnGround ? 0f : 0.3f;
        input = Vector2.zero;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) ResetPlane();
        if (controls.LeverHeld) throttle = controls.Lever;           // 手机推杆：推到哪就是多少
        else throttle = Mathf.Clamp01(throttle + controls.Throttle * throttleRate * Time.deltaTime);   // 键盘：按住慢慢加减
        controls.Lever = throttle;                                  // 推杆跟着显示当前油门

        // 第 6 课：按键、触屏、倾斜都交给 LabInput，这里只拿结果（键盘是 -1 / 0 / 1 的"硬"值，倾斜是中间的值）
        float up = controls.Pitch;
        if (invertPitch) up = -up;
        float right = controls.Roll;
        // 平滑：每帧最多往目标挪 inputSmooth × deltaTime，按下慢慢加满，松开慢慢回零
        input.x = Mathf.MoveTowards(input.x, up, inputSmooth * Time.deltaTime);
        input.y = Mathf.MoveTowards(input.y, right, inputSmooth * Time.deltaTime);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Vector3 v = rb.velocity;
        Vector3 lv = transform.InverseTransformDirection(v);        // 速度换成飞机自己的坐标（第 1 课的 World → Local）
        float fwd = Mathf.Max(0f, lv.z);                            // 往机头方向的速度
        speed = v.magnitude;

        // 操纵：只有俯仰和侧倾，转弯靠侧倾。速度越慢舵面越没劲
        float authority = Mathf.Clamp01(fwd / fullControlSpeed);
        Vector3 delta = new Vector3(-input.x * pitchSpeed, 0f, -input.y * rollSpeed) * authority * dt;
        Quaternion rot = rb.rotation * Quaternion.Euler(delta);     // 第 3 课：乘在右边 = 绕自己的轴转
        if (fwd > 3f)                                               // 往前飞起来才有风向标效应；原地往下掉时不能把机头拽向地面
            rot = Quaternion.Slerp(rot, Quaternion.LookRotation(v, transform.up), noseFollow * dt);
        rb.MoveRotation(rot);

        // ① 推力：朝机头方向
        rb.AddForce(transform.forward * throttle * maxThrust);

        // ② 升力：迎角越大升力越大，到失速角封顶，再用 stallWidth 度慢慢掉到 stallLift 成；方向垂直于速度
        aoa = fwd > 0.5f ? Mathf.Atan2(-lv.y, lv.z) * Mathf.Rad2Deg : 0f;
        stalled = aoa > stallAngle;
        float cl = liftBase + liftPerDegree * Mathf.Min(aoa, stallAngle);
        float fall = stallWidth > 0f ? Mathf.SmoothStep(0f, 1f, (aoa - stallAngle) / stallWidth) : (stalled ? 1f : 0f);
        cl *= Mathf.Lerp(1f, stallLift, fall);
        lift = cl * fwd * fwd;
        rb.AddForce(Vector3.Cross(v, transform.right).normalized * lift);

        // ③ 重力：Rigidbody 自己加

        // ④ 阻力：和速度方向相反，大小 ∝ 速度²
        rb.AddForce(-v * speed * drag);
        rb.AddForce(-transform.right * lv.x * sideGrip);
    }

    void OnGUI()
    {
        GUI.skin.label.fontSize = 22;
        GUI.color = stalled ? Color.red : Color.black;
        float g = -Physics.gravity.y * rb.mass;
        GUILayout.Label($"  速度：{speed * 3.6f:0} km/h");
        GUILayout.Label($"  高度：{transform.position.y:0} m");
        GUILayout.Label($"  油门：{throttle * 100:0}%");
        GUILayout.Label($"  迎角：{aoa:0.0}°" + (stalled ? "　失速！" : ""));
        GUILayout.Label($"  升力 / 重力：{lift / g:0.00}");
        GUILayout.Label("  W/S 俯仰　A/D 侧倾　↑↓ 油门　R 复位");
    }
}
