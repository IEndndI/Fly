using System.Collections.Generic;
using UnityEngine;


public class DialogueLoader : MonoBehaviour
{
    public List<DialogueLine> LoadDialogueFromFile(string fileName)
    {
        List<DialogueLine> result = new List<DialogueLine>();

        TextAsset dataFile = Resources.Load<TextAsset>(fileName);
        string[] lines = dataFile.text.Split('\n');

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd('\r', '\n');
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] fields = line.Split('\t');

            if (fields.Length < 3 && fields.Length > 0)
            {
                Debug.LogWarning("第 " + i + " 行数据格式不完整，已跳过：" + line);
                continue;
            }

            DialogueLine newLine = new DialogueLine();

            if (fields.Length > 0 && !string.IsNullOrEmpty(fields[0]))
            {
                newLine.flow = NormalizeFlow(CleanField(fields[0]));
            }

            string rawName = CleanField(fields[1]);
            if (rawName == "{clear}") newLine.speakerName = "";
            else if (!string.IsNullOrEmpty(rawName)) newLine.speakerName = rawName;

            string rawText = CleanField(fields[2]);
            if (rawText == "{clear}") newLine.text = "";
            else if (!string.IsNullOrEmpty(rawText)) newLine.text = rawText;

            if (fields.Length > 3 && !string.IsNullOrEmpty(fields[3]))
            {
                newLine.characterSpriteName = NormalizeCommand(CleanField(fields[3]));
            }

            if (fields.Length > 4 && !string.IsNullOrEmpty(fields[4]))
            {
                newLine.backgroundSpriteName = NormalizeCommand(CleanField(fields[4]));
            }

            if (fields.Length > 5 && !string.IsNullOrEmpty(fields[5]))
            {
                newLine.hideTarget = NormalizeCommand(CleanField(fields[5]));
            }

            if (fields.Length > 6 && !string.IsNullOrEmpty(fields[6]))
            {
                newLine.effectString = NormalizeCommand(CleanField(fields[6]));
            }

            if (fields.Length > 7 && !string.IsNullOrEmpty(fields[7]))
            {
                newLine.bgmName = NormalizeCommand(CleanField(fields[7]));
            }
            if (fields.Length > 8 && !string.IsNullOrEmpty(fields[8]))
            {
                newLine.ambient = NormalizeCommand(CleanField(fields[8]));
            }
            if (fields.Length > 9 && !string.IsNullOrEmpty(fields[9]))
            {
                newLine.lineType = NormalizeCommand(CleanField(fields[9]));
            }

            result.Add(newLine);
        }

        return result;
    }

    // 去掉表格软件自动加的外层引号
    string CleanField(string raw)
    {
        string s = raw.Trim();
        if (s.Length >= 2 && s.StartsWith("\"") && s.EndsWith("\""))
        {
            s = s.Substring(1, s.Length - 2);
            s = s.Replace("\"\"", "\"");
        }
        return s;
    }

    // 指令列：中文输入法打出的全角符号统一换成半角，后面的代码只认半角
    string NormalizeCommand(string s)
    {
        return s.Replace('：', ':')
                .Replace('，', ',')
                .Replace('（', '(')
                .Replace('）', ')')
                .Replace('【', '[')
                .Replace('】', ']')
                .Replace('。', '.');   // 0。3 → 0.3
    }

    // flow 列：只换关键字后面那个冒号和选项的 > ，option 的按钮文字是给玩家看的，里面的标点要留着
    string NormalizeFlow(string s)
    {
        int full = s.IndexOf('：');
        int half = s.IndexOf(':');
        if (full >= 0 && (half < 0 || full < half))
        {
            s = s.Substring(0, full) + ":" + s.Substring(full + 1);   // label：风吹 → label:风吹
        }

        if (s.StartsWith("option:"))
        {
            s = s.Replace('＞', '>').Replace('》', '>');
        }
        return s;
    }
}