using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

public sealed class XlsxRow
{
    public int RowNumber;      // Excel 화면에 보이는 행 번호 (1부터)
    public string[] Cells;     // 0번 = A열. 빈 칸은 ""
}

public sealed class XlsxSheet
{
    public string Name;
    public List<XlsxRow> Rows = new List<XlsxRow>();
}

/// <summary>
/// 외부 라이브러리 없이 .xlsx를 읽는 최소 리더.
/// 값(캐시된 결과)만 읽는다. 수식 자체나 서식은 읽지 않는다.
/// UnityEngine에 의존하지 않는다.
/// </summary>
public static class XlsxReader
{
    static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    static readonly XNamespace RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace PkgRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>시트 이름 → 시트. Excel이 파일을 열어둔 상태여도 읽을 수 있다.</summary>
    public static Dictionary<string, XlsxSheet> Read(string path)
    {
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
        {
            var shared = ReadSharedStrings(zip);
            var rels = ReadWorkbookRelationships(zip);
            var wb = LoadXml(zip, "xl/workbook.xml");
            if (wb == null) throw new InvalidDataException("xl/workbook.xml이 없습니다. .xlsx 파일이 맞는지 확인하세요.");

            var result = new Dictionary<string, XlsxSheet>();
            var sheets = wb.Root.Element(Main + "sheets");
            if (sheets == null) return result;

            foreach (var s in sheets.Elements(Main + "sheet"))
            {
                string name = (string)s.Attribute("name");
                string rid = (string)s.Attribute(RelNs + "id");
                string target;
                if (name == null || rid == null || !rels.TryGetValue(rid, out target)) continue;

                string entryPath = target.StartsWith("/") ? target.TrimStart('/') : "xl/" + target;
                result[name] = ReadSheet(zip, entryPath, name, shared);
            }
            return result;
        }
    }

    // ---------- 내부 ----------

    static XDocument LoadXml(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path) ?? zip.GetEntry(path.Replace('/', '\\'));
        if (entry == null) return null;
        using (var st = entry.Open()) return XDocument.Load(st);
    }

    static List<string> ReadSharedStrings(ZipArchive zip)
    {
        var list = new List<string>();
        var doc = LoadXml(zip, "xl/sharedStrings.xml");
        if (doc == null) return list;
        foreach (var si in doc.Root.Elements(Main + "si")) list.Add(ReadRichText(si));
        return list;
    }

    static Dictionary<string, string> ReadWorkbookRelationships(ZipArchive zip)
    {
        var map = new Dictionary<string, string>();
        var doc = LoadXml(zip, "xl/_rels/workbook.xml.rels");
        if (doc == null) return map;
        foreach (var r in doc.Root.Elements(PkgRelNs + "Relationship"))
        {
            string id = (string)r.Attribute("Id");
            string target = (string)r.Attribute("Target");
            if (id != null && target != null) map[id] = target;
        }
        return map;
    }

    // <si> 또는 <is> 안의 텍스트. 서식 조각(<r>)은 이어붙이고 후리가나(<rPh>)는 무시한다.
    static string ReadRichText(XElement e)
    {
        var sb = new StringBuilder();
        foreach (var n in e.Elements())
        {
            if (n.Name == Main + "t") sb.Append(n.Value);
            else if (n.Name == Main + "r")
            {
                var t = n.Element(Main + "t");
                if (t != null) sb.Append(t.Value);
            }
        }
        return DecodeEscapes(sb.ToString());
    }

    // Excel은 제어문자를 _x000D_ 형태로 저장한다.
    static string DecodeEscapes(string s)
    {
        if (s.IndexOf("_x", StringComparison.Ordinal) < 0) return s;
        return Regex.Replace(s, "_x([0-9A-Fa-f]{4})_", m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
    }

    static XlsxSheet ReadSheet(ZipArchive zip, string path, string name, List<string> shared)
    {
        var sheet = new XlsxSheet { Name = name };
        var doc = LoadXml(zip, path);
        if (doc == null) return sheet;
        var data = doc.Root.Element(Main + "sheetData");
        if (data == null) return sheet;

        int lastRow = 0;
        foreach (var row in data.Elements(Main + "row"))
        {
            int rowNum = (int?)row.Attribute("r") ?? (lastRow + 1);
            lastRow = rowNum;

            var cells = new List<string>();
            foreach (var c in row.Elements(Main + "c"))
            {
                int col = ColumnIndex((string)c.Attribute("r"), cells.Count);
                while (cells.Count <= col) cells.Add("");
                cells[col] = ReadCell(c, shared);
            }
            sheet.Rows.Add(new XlsxRow { RowNumber = rowNum, Cells = cells.ToArray() });
        }
        return sheet;
    }

    static string ReadCell(XElement c, List<string> shared)
    {
        string t = (string)c.Attribute("t");
        if (t == "inlineStr")
        {
            var isEl = c.Element(Main + "is");
            return isEl == null ? "" : ReadRichText(isEl);
        }
        string v = (string)c.Element(Main + "v") ?? "";
        if (t == "s")
        {
            int idx;
            return int.TryParse(v, out idx) && idx >= 0 && idx < shared.Count ? shared[idx] : "";
        }
        if (t == "b") return v == "1" ? "TRUE" : "FALSE";
        if (t == "str") return DecodeEscapes(v);
        return v; // 숫자 등
    }

    // "B12" → 1 (0-based). 참조가 없으면 fallback 사용.
    static int ColumnIndex(string cellRef, int fallback)
    {
        if (string.IsNullOrEmpty(cellRef)) return fallback;
        int col = 0, i = 0;
        while (i < cellRef.Length && char.IsLetter(cellRef[i]))
        {
            col = col * 26 + (char.ToUpperInvariant(cellRef[i]) - 'A' + 1);
            i++;
        }
        return i == 0 ? fallback : col - 1;
    }
}