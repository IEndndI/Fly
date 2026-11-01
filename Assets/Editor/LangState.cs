using UnityEditor;
using UnityEngine;

// 测试用：清掉已选的语言，下次按 Play 会重新出现选语言界面
public static class LangState
{
    [MenuItem("Tools/语言：重新选择（清掉已选的语言）")]
    static void Reset()
    {
        PlayerPrefs.DeleteKey("Fly.Lang"); PlayerPrefs.Save();
        Debug.Log("已清掉语言选择，下次打开会重新问");
    }
}
