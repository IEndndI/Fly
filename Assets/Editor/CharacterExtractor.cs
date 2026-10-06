using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor; // 编辑器专用工具箱，只有 Editor 脚本才能用
using UnityEngine;

public class CharacterExtractor
{
    // 在 Unity 顶部菜单栏里，新增一个自定义按钮
    [MenuItem("Tools/提取台词中的所有汉字")]
    public static void ExtractCharacters()
    {
        // 用一个 HashSet 存放字符——它的特点是"自动去重"，加入重复的字符不会真的加进去
        HashSet<char> uniqueChars = new HashSet<char>();

        // 读取台词表格文件（跟 DialogueLoader 读取方式一样）
        TextAsset dataFile = Resources.Load<TextAsset>("Fly");
        if (dataFile == null)
        {
            Debug.LogError("找不到 TestDialogue 文件，检查路径是否正确");
            return;
        }

        string[] lines = dataFile.text.Split('\n');

        for (int i = 1; i < lines.Length; i++) // 跳过第0行标题
        {
            string line = lines[i].TrimEnd('\r', '\n'); // 只去掉行尾换行符，保留开头的 Tab
            if (string.IsNullOrWhiteSpace(line)) continue; // 整行都是空白才跳过

            string[] fields = line.Split('\t');
            if (fields.Length < 3) continue;
            
            if (fields[0].StartsWith("option:"))
            {
                string body = fields[0].Substring(7);
                body = body.Replace('＞', '>').Replace('》', '>');
                string[] parts = body.Split('>');
                foreach (char c in parts[0])
                {
                    uniqueChars.Add(c);
                }
            }

            // 把角色名(fields[1])和台词(fields[2])里的每一个字，都丢进这个"去重集合"里
            string cleanSpeakerName = fields[1].Replace("{w}", "").Replace("{nw}", "").Replace("{clear}", "");
            foreach (char c in cleanSpeakerName)
            {
                uniqueChars.Add(c);
            }
            string cleanText = fields[2].Replace("{w}", "").Replace("{nw}", "").Replace("{clear}", "");
            foreach (char c in cleanText)
            {
                uniqueChars.Add(c);
            }
        }

        uniqueChars.Add('_');

        // 把所有不重复的字符，拼成一整段文字
        StringBuilder sb = new StringBuilder();
        foreach (char c in uniqueChars)
        {
            sb.Append(c);
        }

        // 写入一个新文件，保存在 Assets 根目录下，命名 AllCharacters.txt
        string outputPath = Application.dataPath + "/AllCharacters.txt";
        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);

        Debug.Log("提取完成！一共找到 " + uniqueChars.Count + " 个不重复字符，已保存到：" + outputPath);

        AssetDatabase.Refresh(); // 让 Unity 重新扫描一下 Project 窗口，能立刻看到新生成的文件
    }
}