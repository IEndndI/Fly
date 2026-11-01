using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TMPro;

// 日文字体烘焙（v1.3）：把日文版会用到的字一次性画进图集，然后锁成「静态」。
// 为什么不用动态：动态是第一次用到哪个字才临时去画，打开字多的界面会卡一下；而且打包要带上整个 ttf（多 20MB）。
// 字从哪来：Loc/story.txt、Loc/ui.txt 的日文列，About_ja、EndCredits_ja，加上英文字母、数字、常用标点。
// 改了日文译文以后，点一次菜单 Tools → 语言：生成日文字体（静态图集）。没画进去的字会借中文字体显示，不会变成方块。
static class LocFontsBake
{
    const string Dir = "Assets/Data/Resources/Fonts/ja";

    [MenuItem("Tools/语言：生成日文字体（静态图集）")]
    static void Bake()
    {
        string chars = Chars();
        // 行高、上沿照中文那套字体：日文字体自带的行距大（字号的 1.45 倍，文楷是 1.18 倍），不改的话三行字会压到对话框下面的小字
        // 比例（行高、上沿、下沿 ÷ 字号）是从中文字体量出来的：MainFont 1.172 / 0.928 / -0.244，文楷 1.184 / 0.928 / -0.256
        Do("ZenKakuBold SDF", chars, 1.172f, 0.928f, -0.244f);
        Do("Klee SDF", chars, 1.184f, 0.928f, -0.256f);
        Do("KleeSemi SDF", chars, 1.184f, 0.928f, -0.256f);
        AssetDatabase.SaveAssets();
        Debug.Log("日文字体：画进了 " + chars.Length + " 个字");
    }

    static void Do(string name, string chars, float line, float asc, float desc)
    {
        var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Dir + "/" + name + ".asset");
        if (f == null) { Debug.LogWarning("找不到字体：" + name); return; }
        var so = new SerializedObject(f);                                  // 三套都用 4096 的图集（约 1800 字的位置），够用还有富余
        so.FindProperty("m_AtlasWidth").intValue = 4096; so.FindProperty("m_AtlasHeight").intValue = 4096;
        so.ApplyModifiedPropertiesWithoutUndo();
        f.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        f.ClearFontAssetData(true);
        f.TryAddCharacters(chars, out string missing);
        var fi = f.faceInfo;
        fi.lineHeight = fi.pointSize * line;
        fi.ascentLine = fi.pointSize * asc;
        fi.descentLine = fi.pointSize * desc;
        f.faceInfo = fi;
        f.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(f);
        if (!string.IsNullOrEmpty(missing)) Debug.LogWarning(name + " 里没有这些字（会借中文字体）：" + missing);
    }

    static string Chars()
    {
        var set = new HashSet<char>();
        void Add(string s) { foreach (char c in s) if (!char.IsControl(c)) set.Add(c); }
        void Column(string path, int col)
        {
            var ta = Resources.Load<TextAsset>(path); if (ta == null) return;
            foreach (var line in ta.text.Split('\n'))
            {
                if (line.StartsWith("#")) continue;
                var c = line.TrimEnd('\r').Split('\t');
                if (c.Length > col) Add(c[col]);
            }
        }
        Column("Loc/story", 2);
        Column("Loc/ui", 2);
        foreach (var n in new[] { "About_ja", "EndCredits_ja" }) { var ta = Resources.Load<TextAsset>(n); if (ta != null) Add(ta.text); }
        for (char c = ' '; c <= '~'; c++) set.Add(c);
        Add("、。，．・：；？！「」『』（）［］【】〈〉《》…‥—―－～〜・々ー゛゜“”‘’→←↑↓×÷±°％＆＊＋＝／＼｜＃＠№0123456789０１２３４５６７８９ 　");
        return new string(set.OrderBy(c => c).ToArray());
    }
}
