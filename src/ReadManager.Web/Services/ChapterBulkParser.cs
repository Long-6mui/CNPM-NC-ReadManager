using System.Text.RegularExpressions;

namespace ReadManager.Web.Services;

public record ParsedChapter(int No, string Title, string Content);

/// <summary>
/// Nhận diện dòng tiêu đề dạng "Chương 3: Tên chương" / "Chương 3 - Tên" / "### Chương 3 Tên"
/// (không phân biệt hoa/thường), tách văn bản dán vào thành từng chương riêng.
/// </summary>
public static class ChapterBulkParser
{
    private static readonly Regex HeadRegex = new(
        @"^\s*#{0,3}\s*ch[uư][oơ]?ng\s+(\d+)\s*[:\-–.]?\s*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<ParsedChapter> Parse(string raw)
    {
        var lines = (raw ?? "").Replace("\r\n", "\n").Split('\n');
        var chunks = new List<(int No, string Title, List<string> Lines)>();
        (int No, string Title, List<string> Lines)? current = null;

        foreach (var line in lines)
        {
            var m = HeadRegex.Match(line);
            if (m.Success)
            {
                if (current.HasValue) chunks.Add(current.Value);
                if (!int.TryParse(m.Groups[1].Value, out int no) || no < 1) { current = null; continue; }
                string title = m.Groups[2].Value.Trim();
                current = (no, string.IsNullOrEmpty(title) ? $"Chương {no}" : title, new List<string>());
            }
            else
            {
                current?.Lines.Add(line);
            }
        }
        if (current.HasValue) chunks.Add(current.Value);

        return chunks
            .Select(c => new ParsedChapter(c.No, c.Title, string.Join("\n", c.Lines).Trim()))
            .Where(c => c.Content.Length > 0)
            .OrderBy(c => c.No)
            .ToList();
    }
}
