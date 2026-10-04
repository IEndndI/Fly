using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 图片索引：扫描 Resources/Sprites 下所有子文件夹里的图片，生成「文件名 → 路径」对照表 SpriteIndex.txt。
// 游戏里查这张表，表格里就只需要写文件名（room、Cus_lie），不用管图放在哪个子文件夹——类似 Ren'Py 的 images/。
// 图片有增删改 → 自动重建；也可以点菜单 Tools → 重建图片索引。
public class SpriteIndexer : AssetPostprocessor
{
    const string SpriteFolder = "Assets/Data/Resources/Sprites";
    const string IndexPath = "Assets/Data/Resources/SpriteIndex.txt";
    static readonly string[] ImageExts = { ".png", ".jpg", ".jpeg", ".psd", ".tga" };

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        // 只要 Sprites 里有图片变动，就整张表重建一次
        foreach (string[] list in new[] { imported, deleted, moved, movedFrom })
            foreach (string path in list)
                if (IsSpriteImage(path)) { Build(); return; }
    }

    [MenuItem("Tools/重建图片索引")]
    static void Build()
    {
        var index = new SortedDictionary<string, string>();   // 文件名 → Resources 下的路径（不带扩展名）
        var sb = new StringBuilder();
        foreach (string file in Directory.GetFiles(SpriteFolder, "*.*", SearchOption.AllDirectories))
        {
            string path = file.Replace('\\', '/');
            if (!IsSpriteImage(path)) continue;
            string name = Path.GetFileNameWithoutExtension(path);
            string loadPath = path.Substring("Assets/Data/Resources/".Length);
            loadPath = loadPath.Substring(0, loadPath.Length - Path.GetExtension(loadPath).Length);
            if (index.TryGetValue(name, out string other))
            {
                Debug.LogWarning("图片重名：" + name + "\n  " + other + "\n  " + loadPath + "\n表格里写名字时只会用到第一个，请改名。");
                continue;
            }
            index[name] = loadPath;
        }
        foreach (var pair in index) sb.Append(pair.Key).Append('\t').Append(pair.Value).Append('\n');

        string text = sb.ToString();
        if (File.Exists(IndexPath) && File.ReadAllText(IndexPath) == text) return;   // 没变化就不写，免得反复导入
        File.WriteAllText(IndexPath, text, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(IndexPath);
        Debug.Log("图片索引已更新：" + index.Count + " 张 → " + IndexPath);
    }

    static bool IsSpriteImage(string path)
    {
        if (!path.StartsWith(SpriteFolder + "/")) return false;
        return Array.IndexOf(ImageExts, Path.GetExtension(path).ToLowerInvariant()) >= 0;
    }
}
