using UnityEngine;

// 第 6 课：输入。把"玩家想干什么"（俯仰、侧倾、油门）和"玩家怎么表达"（键盘、触屏）分开。
// LabFly 只问这里要数，不关心它们是从哪来的。以后加手柄，只改这个文件。
// 手机：左下是固定摇杆（上下 = 俯仰，左右 = 侧倾），右边是油门推杆（推到哪就是多少）
public class LabInput : MonoBehaviour
{
    [Header("摇杆（位置、大小都按屏幕高度的比例）")]
    public Vector2 stickCenter = new Vector2(0.28f, 0.3f);   // 中心：离左边 0.28 个屏高，离底边 0.3 个屏高
    public float stickRadius = 0.15f;                         // 推到边 = 满舵
    [Range(0f, 0.5f)] public float stickDeadZone = 0.1f;      // 死区：中心附近一小圈当作 0

    [Header("油门推杆")]
    public float leverX = 0.12f;                              // 离右边多远（屏高比例）
    public Vector2 leverRange = new Vector2(0.15f, 0.85f);    // 推杆从屏幕多高到多高

    [Header("编辑器里预览触屏的界面（鼠标当手指）")]
    public bool previewTouchUI = false;

    // ↓ LabFly 读这些。Pitch、Roll：-1 ~ 1；Throttle：键盘的 -1 / 0 / 1（按住加减）
    public float Pitch { get; private set; }
    public float Roll { get; private set; }
    public float Throttle { get; private set; }
    // 推杆：手指按着时 LeverHeld = true，Lever 就是油门（0 ~ 1）；没按着时 LabFly 把当前油门写回来，推杆跟着显示
    public bool LeverHeld { get; private set; }
    public float Lever { get; set; }

    int stickFinger = -1, leverFinger = -1;
    Vector2 knob;                                             // 摇杆头的偏移，-1 ~ 1

    bool TouchUI => Application.isMobilePlatform || previewTouchUI;
    float H => Screen.height;
    Vector2 StickPos => new Vector2(stickCenter.x * H, stickCenter.y * H);
    float LeverPx => Screen.width - leverX * H;

    void Update()
    {
        // 1. 键盘（电脑）
        float kPitch = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
        float kRoll = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                    - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        Throttle = (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);

        // 2. 触屏（手机）
        knob = Vector2.zero; LeverHeld = false;
        if (TouchUI) ReadTouch();

        // 3. 合并：哪个推得更用力听哪个
        Vector2 s = ApplyDeadZone(knob);
        Pitch = Mathf.Abs(kPitch) >= Mathf.Abs(s.y) ? kPitch : s.y;
        Roll = Mathf.Abs(kRoll) >= Mathf.Abs(s.x) ? kRoll : s.x;
    }

    void ReadTouch()
    {
        bool stickAlive = false, leverAlive = false;
        int count = Input.touchCount;
        bool mouse = count == 0 && previewTouchUI && Input.GetMouseButton(0);   // 编辑器里用鼠标模拟一根手指

        for (int i = 0; i < (mouse ? 1 : count); i++)
        {
            Vector2 pos; int id; bool began;
            if (mouse) { pos = Input.mousePosition; id = 99; began = Input.GetMouseButtonDown(0); }
            else { var t = Input.GetTouch(i); pos = t.position; id = t.fingerId; began = t.phase == TouchPhase.Began; }

            // 手指刚按下：看按在哪个控件上，就"认领"给它。之后手指滑出去也还算它的
            if (began)
            {
                if (stickFinger < 0 && Vector2.Distance(pos, StickPos) < stickRadius * H * 1.6f) stickFinger = id;
                else if (leverFinger < 0 && Mathf.Abs(pos.x - LeverPx) < 0.1f * H) leverFinger = id;
            }

            if (id == stickFinger)
            {
                stickAlive = true;
                knob = Vector2.ClampMagnitude((pos - StickPos) / (stickRadius * H), 1f);
            }
            else if (id == leverFinger)
            {
                leverAlive = true; LeverHeld = true;
                Lever = Mathf.InverseLerp(leverRange.x * H, leverRange.y * H, pos.y);
            }
        }
        if (!stickAlive) stickFinger = -1;    // 手指松开：摇杆回中
        if (!leverAlive) leverFinger = -1;    // 推杆不回中，停在原处
    }

    // 死区：中心一小圈当 0，圈外重新从 0 算到 1，不然一出死区就猛地跳一下
    Vector2 ApplyDeadZone(Vector2 v)
    {
        float m = v.magnitude;
        if (m < stickDeadZone) return Vector2.zero;
        return v / m * ((m - stickDeadZone) / (1f - stickDeadZone));
    }

    void OnGUI()
    {
        if (!TouchUI) return;
        // 注意：GUI 的 y 从上往下数，触屏的 y 从下往上数，画的时候要翻过来
        float r = stickRadius * H;
        Vector2 c = new Vector2(StickPos.x, H - StickPos.y);
        GUI.color = new Color(0f, 0f, 0f, 0.15f);
        GUI.Box(new Rect(c.x - r, c.y - r, r * 2, r * 2), "");
        GUI.color = new Color(0f, 0.28f, 0.67f, 0.7f);
        float k = r * 0.35f;
        GUI.Box(new Rect(c.x + knob.x * r - k, c.y - knob.y * r - k, k * 2, k * 2), "");

        float top = H - leverRange.y * H, bottom = H - leverRange.x * H;
        GUI.color = new Color(0f, 0f, 0f, 0.15f);
        GUI.Box(new Rect(LeverPx - 0.03f * H, top, 0.06f * H, bottom - top), "");
        float y = Mathf.Lerp(bottom, top, Lever);
        GUI.color = new Color(0.85f, 0.25f, 0.2f, 0.85f);
        GUI.Box(new Rect(LeverPx - 0.07f * H, y - 0.025f * H, 0.14f * H, 0.05f * H), "");
        GUI.color = Color.black;
        var st = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(0.03f * H), alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(LeverPx - 0.1f * H, top - 0.07f * H, 0.2f * H, 0.05f * H), $"油门 {Lever * 100:0}%", st);
    }
}
