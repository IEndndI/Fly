using UnityEditor;
using UnityEngine;

// 创作声明页：切换"第一次打开 / 已经看过"，测试用。打了勾 = 已经看过（进来七项直接落好）
public static class DisclosureState
{
    const string Key = "Fly.DisclosureDone", Path = "Tools/创作声明：已看过（取消勾选 = 第一次打开）";

    [MenuItem(Path)]
    static void Toggle()
    {
        bool seen = PlayerPrefs.GetInt(Key, 0) == 1;
        PlayerPrefs.SetInt(Key, seen ? 0 : 1); PlayerPrefs.Save();
        Debug.Log(seen ? "创作声明：已重置为「第一次打开」" : "创作声明：已设为「看过了」");
    }

    [MenuItem(Path, true)]
    static bool Check() { Menu.SetChecked(Path, PlayerPrefs.GetInt(Key, 0) == 1); return true; }
}
