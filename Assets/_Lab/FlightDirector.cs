using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 第 7 课：飞行场景的"导演"。按模式摆好参数，判断什么时候算飞完，淡出以后告诉剧情。
// 被剧情叫起来时（fly3d:first / second）听 FlightSession.Mode；在编辑器里直接打开这个场景测试时听 testMode。
public class FlightDirector : MonoBehaviour
{
    public LabFly fly;
    public LabCamFx camFx;
    public string testMode = "first";

    [Header("first：第一次飞行")]
    public float firstThrust = 3f;
    public float goalHeight = 120f;         // 飞到这么高算飞完（以后换成"飞出宇宙"）

    [Header("second：第二次飞行")]
    public float secondThrust = 2f;
    public float triedThrottle = 0.3f;      // 油门推过这么多，才算"试过了"
    public float giveUpSeconds = 3f;        // 试过以后，油门拉回 0 保持这么久 = 放弃

    [Header("淡入淡出（秒）")]
    public float fadeIn = 1.5f;
    public float fadeOut = 2f;

    string mode;
    bool tried, ending;
    float idle;                             // 油门在 0 待了多久
    float black = 1f;                       // 黑幕：1 = 全黑
    Texture2D px;

    void Start()
    {
        mode = FlightSession.Launched ? FlightSession.Mode : testMode;
        bool second = mode == "second";
        fly.maxThrust = second ? secondThrust : firstThrust;
        camFx.forceFront = second;          // 你定的：第二次一直在正面
        px = Texture2D.whiteTexture;
    }

    void Update()
    {
        if (!ending) black = Mathf.MoveTowards(black, 0f, Time.deltaTime / fadeIn);
        if (ending) return;

        if (mode == "second")
        {
            if (fly.Throttle >= triedThrottle) tried = true;
            idle = tried && fly.Throttle <= 0.01f ? idle + Time.deltaTime : 0f;
            if (idle >= giveUpSeconds) StartCoroutine(End());
        }
        else if (fly.transform.position.y >= goalHeight) StartCoroutine(End());
    }

    IEnumerator End()
    {
        ending = true;
        while (black < 1f) { black = Mathf.MoveTowards(black, 1f, Time.deltaTime / fadeOut); yield return null; }
        yield return new WaitForSeconds(0.5f);
        if (FlightSession.Launched) FlightSession.Finished = true;           // 告诉剧情：飞完了，FlightSession 会把这个场景卸掉
        else SceneManager.LoadScene(gameObject.scene.name);                  // 单独测试：从头再来
    }

    void OnGUI()
    {
        GUI.depth = -100;                   // 画在其它 GUI 上面
        if (black > 0f)
        {
            GUI.color = new Color(0f, 0f, 0f, black);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), px);
        }
        if (!FlightSession.Launched)        // 测试时才显示：现在是什么模式、离结束还差多少
        {
            GUI.color = new Color(0f, 0.28f, 0.67f);
            var st = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.UpperRight };
            string info = mode == "second"
                ? $"模式 second　试过了：{(tried ? "是" : "否")}　油门归零 {idle:0.0} / {giveUpSeconds} 秒"
                : $"模式 first　高度 {fly.transform.position.y:0} / {goalHeight} 米";
            GUI.Label(new Rect(0, 8, Screen.width - 16, 30), info, st);
        }
    }
}
