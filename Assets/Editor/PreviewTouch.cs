using UnityEditor;

// 在编辑器里预览手机版的大按钮（快捷栏、返回键）：勾上以后进 Play 就是手机样式
public static class PreviewTouch
{
    const string Key = "Fly.PreviewTouch", Path = "Tools/预览手机版按钮";

    [MenuItem(Path)]
    static void Toggle() { EditorPrefs.SetBool(Key, !EditorPrefs.GetBool(Key, false)); }

    [MenuItem(Path, true)]
    static bool Check() { Menu.SetChecked(Path, EditorPrefs.GetBool(Key, false)); return true; }
}
