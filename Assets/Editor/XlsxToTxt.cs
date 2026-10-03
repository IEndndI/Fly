using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;   // xlsx 其实是个 zip 压缩包
using System.Linq;
using System.Text;
using System.Xml.Linq;         // 压缩包里装的是 XML
using UnityEditor;
using UnityEngine;

// 把 Assets/Data/ 下的 xlsx 台词表转成 Assets/Data/Resources/ 里同名的 UTF-8 制表符 txt。
// WPS 里保存 → 切回 Unity，会自动转换；也可以点菜单 Tools → 表格转 txt 手动全部重转。
public class XlsxToTxt : AssetPostprocessor
{
    const string SourceFolder = "Assets/Data";
    const string OutputFolder = "Assets/Data/Resources";

    static readonly XNamespace ns     = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    static readonly XNamespace relNs  = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace pkgNs  = "http://schemas.openxmlformats.org/package/2006/relationships";

    // Unity 每次导入/重新导入资源后都会调用这里
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string path in imported)
        {
            if (IsDialogueSheet(path)) Convert(path);
        }
    }

    [MenuItem("Tools/表格转 txt（全部重转）")]
    static void ConvertAll()
    {
        foreach (string file in Directory.GetFiles(SourceFolder, "*.xlsx"))
        {
            string path = file.Replace('\\', '/');
            if (IsDialogueSheet(path)) Convert(path);
        }
    }

    static bool IsDialogueSheet(string path)
    {
        if (!path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) return false;
        if (Path.GetFileName(path).StartsWith("~$")) return false;                 // WPS 打开表格时生成的锁文件
        return Path.GetDirectoryName(path).Replace('\\', '/') == SourceFolder;     // 只认 Data 根目录下的
    }

    static void Convert(string xlsxPath)
    {
        string txtPath = OutputFolder + "/" + Path.GetFileNameWithoutExtension(xlsxPath) + ".txt";
        try
        {
            List<List<string>> rows = ReadFirstSheet(xlsxPath);
            string text = ToTabText(rows, xlsxPath);
            File.WriteAllText(txtPath, text, new UTF8Encoding(false));   // UTF-8，不带 BOM
            AssetDatabase.ImportAsset(txtPath);
            Debug.Log("表格已转换：" + xlsxPath + " → " + txtPath);
        }
        catch (Exception e)
        {
            Debug.LogError("表格转换失败（WPS 正在保存的话，等它存完再点 Tools → 表格转 txt）：" + xlsxPath + "\n" + e);
        }
    }

    // 读第一个工作表，返回 行 → 列 的二维表
    static List<List<string>> ReadFirstSheet(string xlsxPath)
    {
        // FileShare.ReadWrite：WPS 还开着这个文件时也能读
        using (FileStream fs = new FileStream(xlsxPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read))
        {
            // 文字格子里存的只是编号，真正的文字统一放在 sharedStrings.xml
            List<string> shared = new List<string>();
            ZipArchiveEntry sharedEntry = zip.GetEntry("xl/sharedStrings.xml");
            if (sharedEntry != null)
            {
                foreach (XElement si in LoadXml(sharedEntry).Root.Elements(ns + "si"))
                {
                    // 带格式的文字会被拆成好几段 <t>；rPh 是注音，不要
                    shared.Add(string.Concat(si.Descendants(ns + "t")
                        .Where(t => t.Parent.Name != ns + "rPh")
                        .Select(t => t.Value)));
                }
            }

            // workbook.xml 记着第一张表的 id，rels 记着这个 id 对应哪个文件
            XDocument workbook = LoadXml(zip.GetEntry("xl/workbook.xml"));
            string firstId = workbook.Root.Element(ns + "sheets").Elements(ns + "sheet").First()
                .Attribute(relNs + "id").Value;
            XDocument rels = LoadXml(zip.GetEntry("xl/_rels/workbook.xml.rels"));
            string target = rels.Root.Elements(pkgNs + "Relationship")
                .First(r => (string)r.Attribute("Id") == firstId)
                .Attribute("Target").Value;
            string sheetPath = target.StartsWith("/") ? target.Substring(1) : "xl/" + target;
            XDocument sheet = LoadXml(zip.GetEntry(sheetPath));

            List<List<string>> rows = new List<List<string>>();
            foreach (XElement row in sheet.Root.Element(ns + "sheetData").Elements(ns + "row"))
            {
                // 空行在 XML 里会被省略，所以按 r="行号" 摆到对应位置
                string r = (string)row.Attribute("r");
                int rowIndex = r != null ? int.Parse(r) - 1 : rows.Count;
                while (rows.Count <= rowIndex) rows.Add(new List<string>());
                List<string> cells = rows[rowIndex];

                foreach (XElement c in row.Elements(ns + "c"))
                {
                    // 空格子同样会被省略，按 r="C5" 里的字母定列
                    string cellRef = (string)c.Attribute("r");
                    int col = cellRef != null ? ColumnIndex(cellRef) : cells.Count;
                    while (cells.Count <= col) cells.Add("");
                    cells[col] = CellValue(c, shared);
                }
            }
            return rows;
        }
    }

    static string CellValue(XElement c, List<string> shared)
    {
        string type = (string)c.Attribute("t");
        string v = (string)c.Element(ns + "v");

        switch (type)
        {
            case "s":         return v == null ? "" : shared[int.Parse(v)];               // 文字
            case "inlineStr": return string.Concat(c.Descendants(ns + "t").Select(t => t.Value));
            case "b":         return v == "1" ? "TRUE" : "FALSE";                           // 隐藏列的 TRUE
            case "str":
            case "e":         return v ?? "";                                               // 公式结果 / 错误
            default:
                // 数字：0.3 在 XML 里可能存成 0.29999999999999999，转一下变回 0.3
                if (v != null && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    return d.ToString(CultureInfo.InvariantCulture);
                return v ?? "";
        }
    }

    // "C5" → 2，"AA12" → 26
    static int ColumnIndex(string cellRef)
    {
        int index = 0;
        foreach (char ch in cellRef)
        {
            if (!char.IsLetter(ch)) break;
            index = index * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
        }
        return index - 1;
    }

    static string ToTabText(List<List<string>> rows, string xlsxPath)
    {
        int columnCount = rows.Count > 0 ? rows.Max(r => r.Count) : 0;
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < rows.Count; i++)
        {
            List<string> cells = rows[i];
            for (int col = 0; col < columnCount; col++)
            {
                if (col > 0) sb.Append('\t');
                string cell = col < cells.Count ? cells[col] : "";

                // 格子里的换行/Tab 会把 txt 的行列拆乱，换成空格
                if (cell.IndexOfAny(new[] { '\n', '\r', '\t' }) >= 0)
                {
                    Debug.LogWarning(xlsxPath + " 第 " + (i + 1) + " 行有格子含换行或 Tab，已换成空格");
                    cell = cell.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');
                }
                sb.Append(cell);
            }
            sb.Append("\r\n");
        }
        return sb.ToString();
    }

    static XDocument LoadXml(ZipArchiveEntry entry)
    {
        using (Stream s = entry.Open()) return XDocument.Load(s);
    }
}
