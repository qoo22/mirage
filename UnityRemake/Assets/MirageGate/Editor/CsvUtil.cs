using System.Collections.Generic;
using System.Text;

namespace MirageGate.EditorTools
{
    /// <summary>
    /// 最小限のCSVパーサ。ダブルクオート囲み・"" エスケープ・改行を含むフィールドに対応。
    /// 行頭が # の行はコメントとして無視する。
    /// </summary>
    public static class CsvUtil
    {
        /// <summary>CSVテキストを行→セル配列に分解（ヘッダ含む）。コメント行は除外。</summary>
        public static List<string[]> Parse(string text)
        {
            var rows = new List<string[]>();
            var cur = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else sb.Append(ch);
                }
                else
                {
                    if (ch == '"') inQuotes = true;
                    else if (ch == ',') { cur.Add(sb.ToString()); sb.Clear(); }
                    else if (ch == '\n') { cur.Add(sb.ToString()); sb.Clear(); AddRow(rows, cur); cur = new List<string>(); }
                    else sb.Append(ch);
                }
            }
            if (sb.Length > 0 || cur.Count > 0) { cur.Add(sb.ToString()); AddRow(rows, cur); }
            return rows;
        }

        static void AddRow(List<string[]> rows, List<string> cur)
        {
            if (cur.Count == 1 && string.IsNullOrWhiteSpace(cur[0])) return; // 空行
            if (cur.Count > 0 && cur[0].TrimStart().StartsWith("#")) return; // コメント
            rows.Add(cur.ToArray());
        }

        /// <summary>ヘッダ行を使い、各データ行を 列名→値 の辞書に変換。</summary>
        public static List<Dictionary<string, string>> ParseWithHeader(string text)
        {
            var rows = Parse(text);
            var result = new List<Dictionary<string, string>>();
            if (rows.Count == 0) return result;
            var header = rows[0];
            for (int r = 1; r < rows.Count; r++)
            {
                var dict = new Dictionary<string, string>();
                for (int c = 0; c < header.Length && c < rows[r].Length; c++)
                    dict[header[c].Trim()] = rows[r][c];
                result.Add(dict);
            }
            return result;
        }
    }
}
