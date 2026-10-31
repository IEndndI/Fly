using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 剧情 ↔ 3D 飞行的桥。表格 effect 列写 fly3d:first / fly3d:second。
// 流程：VN 让位（关掉自己的摄像机、耳朵、界面、按键）→ 叠加加载飞行场景 → 等飞行场景说"飞完了" → 卸载 → VN 复原。
// static：两个场景都能直接找到它，不用拖槽位
public static class FlightSession
{
    public const string SceneName = "FlightLab";
    public const bool Enabled = false;            // v1.2 正式版：3D 飞行还没做完，表格里就算写了 fly3d 也跳过。做完改成 true，并把 FlightLab 加回打包列表

    public static string Mode = "first";          // 剧本传进来的：first / second。飞行场景开场时读
    public static bool Running { get; private set; }
    public static bool Launched { get; private set; }   // 是被剧情叫起来的（不是在编辑器里直接打开飞行场景测试）
    public static bool Finished;                  // 飞行场景飞完了就把它设成 true

    public static IEnumerator Run(string mode)
    {
        Running = true; Launched = true; Finished = false;
        Mode = string.IsNullOrEmpty(mode) ? "first" : mode;
        Scene vn = SceneManager.GetActiveScene();

        // 1. VN 让位：摄像机、AudioListener（耳朵，一个场景只能有一个）、界面全关掉，记下来之后再开
        var off = new List<Behaviour>();
        foreach (GameObject root in vn.GetRootGameObjects())
            foreach (Behaviour b in root.GetComponentsInChildren<Behaviour>())
                if ((b is Camera || b is AudioListener || b is Canvas) && b.enabled) { b.enabled = false; off.Add(b); }
        SysPanels.UiOpen++;                       // DialogueManager 看到"有页开着"就不响应点击、A、Esc

        // 2. 叠加加载：VN 场景不关，飞行场景放在它旁边
        yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
        Scene fly = SceneManager.GetSceneByName(SceneName);
        SceneManager.SetActiveScene(fly);         // 雾、环境光按"活动场景"的设置来

        // 3. 等飞行场景说飞完了
        while (!Finished) yield return null;

        // 4. 收场：切回 VN、卸载飞行场景、把关掉的东西开回来
        SceneManager.SetActiveScene(vn);
        yield return SceneManager.UnloadSceneAsync(fly);
        foreach (Behaviour b in off) if (b != null) b.enabled = true;
        SysPanels.UiOpen = Mathf.Max(0, SysPanels.UiOpen - 1);
        Launched = false; Running = false;
    }
}
