using System.Collections.Generic;
using UnityEngine;

// 多语言（v1.3）：中文 zh / 英文 en / 日文 ja。
// 界面文字：代码里照常写中文，外面套一层 Loc.T("存档")；翻译查 Resources/Loc/ui.txt（一行一条：中文<Tab>英文<Tab>日文），查不到就显示中文。
// 剧本：照常读中文 Fly.txt，再用 TranslateLines 按原句查 Resources/Loc/story.txt 换成译文（行号不会错位；新句子没翻就显示中文）。
// 人名只在显示时翻（Loc.Story），因为立绘是按中文 / 原名去找的。
// 整份文件（关于、片尾名单）：Loc.Res("About") 会先找 About_en / About_ja，没有就用中文那份。
// 选过的语言记在 PlayerPrefs 里，第一次打开游戏时由 LangSelect 问。
public static class Loc
{
    public static readonly string[] Codes = { "zh", "en", "ja" };
    const string Key = "Fly.Lang";

    public static bool HasChosen => PlayerPrefs.HasKey(Key);

    public static string Lang
    {
        get => PlayerPrefs.GetString(Key, "zh");
        set { PlayerPrefs.SetString(Key, value); PlayerPrefs.Save(); Changed?.Invoke(); }
    }

    public static event System.Action Changed;          // 换了语言：开着的界面可以自己刷新

    // 第一次打开时默认选中哪一个：跟着系统语言猜
    public static string Guess()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Chinese: case SystemLanguage.ChineseSimplified: case SystemLanguage.ChineseTraditional: return "zh";
            case SystemLanguage.Japanese: return "ja";
            default: return "en";
        }
    }

    static Dictionary<string, string[]> table;

    public static string T(string zh)
    {
        if (string.IsNullOrEmpty(zh) || Lang == "zh") return zh;
        if (table == null) Load();
        int i = System.Array.IndexOf(Codes, Lang);
        return table.TryGetValue(zh, out string[] row) && i < row.Length && row[i] != "" ? row[i] : zh;
    }

    // 拼接句：模板本身也查表（比如 "第{0}章 · {1}" → 英文 "Chapter {2} · {1}"），再把数字、名字填进去
    public static string F(string zhFormat, params object[] args) => string.Format(T(zhFormat), args);

    // 「中文大字 + 英文小字」的标签：英文版里大字已经是英文了，小字就换成中文，免得英文叠英文
    public static string Small(string en, string zh) => Lang == "en" ? zh : en;

    public static string Res(string name)
    {
        if (Lang == "zh") return name;
        string n = name + "_" + Lang;
        return Resources.Load<TextAsset>(n) != null ? n : name;
    }

    static Dictionary<string, string[]> story;

    // 剧本里的一句（台词、人名、选项文字）
    public static string Story(string zh)
    {
        if (string.IsNullOrEmpty(zh) || Lang == "zh") return zh;
        if (story == null) story = Load("Loc/story");
        int i = System.Array.IndexOf(Codes, Lang);
        return story.TryGetValue(zh, out string[] row) && i < row.Length && row[i] != "" ? row[i] : zh;
    }

    // 把读进来的中文剧本就地换成当前语言：台词，和 option:文字>目标 里的文字（目标标签不动）
    public static void TranslateLines(List<DialogueLine> lines)
    {
        if (lines == null || Lang == "zh") return;
        foreach (var l in lines)
        {
            if (!string.IsNullOrEmpty(l.text) && l.text != "{clear}") l.text = Story(l.text);
            if (l.flow != null && l.flow.StartsWith("option:"))
            {
                int gt = l.flow.IndexOf('>');
                if (gt > 7) l.flow = "option:" + Story(l.flow.Substring(7, gt - 7)) + l.flow.Substring(gt);
            }
        }
    }

    static void Load() { table = Load("Loc/ui"); }

    static Dictionary<string, string[]> Load(string path)
    {
        var d = new Dictionary<string, string[]>();
        var ta = Resources.Load<TextAsset>(path);
        if (ta == null) return d;
        foreach (string raw in ta.text.Replace("\r", "").Split('\n'))
        {
            if (raw.Length == 0 || raw.StartsWith("#")) continue;
            string[] c = raw.Split('\t');
            if (c.Length >= 2) d[c[0].Replace("\\n", "\n")] = System.Array.ConvertAll(c, s => s.Replace("\\n", "\n"));
        }
        return d;
    }
}
