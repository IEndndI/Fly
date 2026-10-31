using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// 运行游戏时直接拖立绘调位置，调好了写回 Fly.xlsx（不用去表格里一格格改）
// 1. 运行游戏，演到立绘出来的那一句
// 2. Tools → 立绘 → 选中台上的 Cus（Hierarchy 里会选中它）
// 3. 切到 Scene 窗口，按 T（矩形工具）：拖中间 = 挪位置，拖边角 = 改大小（按住 Shift 等比）
// 4. Tools → 立绘 → 写回表格：只改这一行 / 所有用同一个位置的行
// 运行中改的东西停止后会还原，但已经写进表格了；停止后 Unity 会自动把 xlsx 转成 txt
// 注意：写之前先在 WPS 里关掉 Fly.xlsx，不然 WPS 一保存就把这里写的盖掉
public static class SpritePlacer
{
    const string Xlsx = "Assets/Data/Fly.xlsx";
    const string Sheet = "xl/worksheets/sheet1.xml";

    [MenuItem("Tools/立绘/选中台上的 Cus")]
    static void SelectCus()
    {
        var stage = Object.FindObjectOfType<CharacterStage>();
        RectTransform rt = stage != null ? stage.GetTarget("Cus") : null;
        if (rt == null) { Debug.LogWarning("台上现在没有 Cus"); return; }
        Selection.activeGameObject = rt.gameObject;
        EditorGUIUtility.PingObject(rt.gameObject);
    }

    [MenuItem("Tools/立绘/写回表格：只改这一行")]
    static void WriteThis() { Write(false); }

    [MenuItem("Tools/立绘/写回表格：所有用同一个位置的行")]
    static void WriteSame() { Write(true); }

    [MenuItem("Tools/立绘/选中台上的 Cus", true)]
    [MenuItem("Tools/立绘/写回表格：只改这一行", true)]
    [MenuItem("Tools/立绘/写回表格：所有用同一个位置的行", true)]
    static bool OnlyWhilePlaying() { return EditorApplication.isPlaying; }

    static void Write(bool allSame)
    {
        var dm = Object.FindObjectOfType<DialogueManager>();
        var stage = Object.FindObjectOfType<CharacterStage>();
        RectTransform rt = stage != null ? stage.GetTarget("Cus") : null;
        if (dm == null || rt == null || dm.CurrentIndex < 0) { Debug.LogWarning("台上现在没有 Cus，或者还没开始演"); return; }

        // 往回找：这张立绘是哪一行叫出来的
        int i = dm.CurrentIndex;
        while (i >= 0 && string.IsNullOrEmpty(dm.dialogueLines[i].characterSpriteName)) i--;
        if (i < 0) { Debug.LogWarning("找不到叫出这张立绘的那一行"); return; }
        string oldCell = dm.dialogueLines[i].characterSpriteName.Trim();

        // Unity 坐标 → 表格里的 AE 坐标（左上角原点，y 向下）
        Vector2 p = rt.anchoredPosition;
        string newPlace = $":pos({Mathf.RoundToInt(p.x)},{Mathf.RoundToInt(-p.y)}):h({Mathf.RoundToInt(rt.sizeDelta.y)})";

        int changed = 0;
        using (var fs = new FileStream(Xlsx, FileMode.Open, FileAccess.ReadWrite))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Update))
        {
            ZipArchiveEntry entry = zip.GetEntry(Sheet);
            string xml;
            using (var r = new StreamReader(entry.Open())) xml = r.ReadToEnd();

            for (int k = 0; k < dm.dialogueLines.Count; k++)
            {
                string cell = (dm.dialogueLines[k].characterSpriteName ?? "").Trim();
                bool hit = k == i || (allSame && cell.Length > 0 && Place(cell) == Place(oldCell));   // 位置一样就算（表情可以不同）
                if (!hit) continue;
                string text = Name(cell) + newPlace;                                                   // 各行保留自己的表情
                int row = k + 2;   // 表格第 1 行是表头，dialogueLines[0] 是第 2 行
                var m = Regex.Match(xml, $"<c r=\"D{row}\"[^>]*?(/>|>.*?</c>)", RegexOptions.Singleline);
                if (!m.Success) { Debug.LogWarning($"表格第 {row} 行的 D 格找不到"); continue; }
                xml = xml.Remove(m.Index, m.Length).Insert(m.Index,
                    $"<c r=\"D{row}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{System.Security.SecurityElement.Escape(text)}</t></is></c>");
                changed++;
            }
            entry.Delete();
            using (var w = new StreamWriter(zip.CreateEntry(Sheet).Open())) w.Write(xml);
        }
        Debug.Log($"立绘位置写回表格：{newPlace}（改了 {changed} 行）。停止运行后 Unity 会重新生成 Fly.txt");
    }

    // "Cus_talk:pos(225,1080):h(520)" → 名字 "Cus_talk"、位置 ":pos(225,1080):h(520)"
    static string Name(string cell) { int c = cell.IndexOfAny(new[] { ':', '：' }); return c < 0 ? cell : cell.Substring(0, c); }
    static string Place(string cell) { return cell.Substring(Name(cell).Length); }
}
