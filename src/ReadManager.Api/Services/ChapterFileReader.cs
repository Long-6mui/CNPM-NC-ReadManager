using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace ReadManager.Api.Services;

// PB07–08 — Đọc văn bản dán / file .txt / .zip và tách thành từng chương.
// Chỉ đọc và tách, không đụng database (kiểm tra & lưu nằm ở ChapterService).
// Số chương lấy theo thứ tự: dòng "Chương N" → số trong tên file → tự đánh số.

public class ParsedChapter
{
    public int? ChapterNumber { get; set; }          // null = chưa tìm được số
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;       // lấy từ file nào
    public string NumberFrom { get; set; } = string.Empty;   // "Dòng tiêu đề" | "Tên file" | "Tự đánh số"
    public bool HasError { get; set; }
    public List<string> Problems { get; } = new();
}

public class ChapterReadResult
{
    public List<ParsedChapter> Chapters { get; } = new();
    public List<string> Errors { get; } = new();     // chặn không cho lưu
    public List<string> Warnings { get; } = new();   // vẫn cho lưu
}

public static class ChapterFileReader
{
    public const long MaxTotalReadBytes = 20L * 1024 * 1024;
    public const int MaxParsedChapters = 500;
    private sealed class ReadBudget
    {
        public long Used { get; private set; }
        public void Add(long count)
        {
            if (count > MaxTotalReadBytes - Used)
                throw new InvalidDataException("Tổng nội dung giải nén vượt 20MB. Hãy chia nhỏ lần tải lên.");
            Used += count;
        }
    }
    public const long MaxFileBytes = 10 * 1024 * 1024;        // 10MB / file
    private const long MaxZipEntryBytes = 2 * 1024 * 1024;    // 2MB / file trong zip
    private const int MaxZipEntries = 1000;

    // Dòng tiêu đề: "Chương 5" | "Chương 5: ..." / "- ..." / ". ..." | "Chương 5 Chữ Hoa..."
    // "Chương 5 là ..." (chữ thường) → câu văn, không phải tiêu đề.
    private static readonly Regex HeadingRegex = new(
        @"^\s*#{0,3}\s*[Cc][Hh][UuƯư][OoƠơ]?[Nn][Gg]\s+(?<num>\d+)"
        + @"(?:\s*[:\-–.)]\s*(?<title>.*)"
        + @"|\s+(?<title>[\p{Lu}\d""“'(\[].*)"
        + @"|\s*)$",
        RegexOptions.Compiled);

    // Dòng trông giống tiêu đề nhưng không đúng luật → để cảnh báo
    private static readonly Regex LooksLikeHeadingRegex = new(
        @"^\s*#{0,3}\s*ch[uư][oơ]?ng\s+\d+\s+\S",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DigitsRegex = new(@"\d+", RegexOptions.Compiled);

    // Bỏ "chuong-03 -" ở đầu tên file, phần còn lại làm tiêu đề
    private static readonly Regex FileNamePrefixRegex = new(
        @"^\s*(ch[uư][oơ]?ng|chap(ter)?|ch)?[\s\-_.]*\d+[\s\-_.:–]*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Byte sai (file lỗi font) → báo lỗi thay vì ra dấu ?
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static async Task<ChapterReadResult> ReadAsync(string? pastedText, IReadOnlyList<IFormFile> files)
    {
        var result = new ChapterReadResult();
        var budget = new ReadBudget();
        try
        {

            if (!string.IsNullOrWhiteSpace(pastedText))
            {
                budget.Add(Encoding.UTF8.GetByteCount(pastedText));
                SplitText(pastedText, "Văn bản dán", fileName: null, result);
            }

            // Sắp theo số tự nhiên: 2.txt trước 10.txt
            foreach (var file in files.OrderBy(f => f.FileName, NaturalComparer.Instance))
            {
                var name = Path.GetFileName(file.FileName);
                var ext = Path.GetExtension(name).ToLowerInvariant();

                if (file.Length == 0)
                {
                    result.Errors.Add($"File \"{name}\" rỗng, không có nội dung.");
                    continue;
                }
                if (file.Length > MaxFileBytes)
                {
                    result.Errors.Add($"File \"{name}\" lớn hơn 10MB.");
                    continue;
                }

                if (ext == ".txt")
                {
                    await using var stream = file.OpenReadStream();
                    var text = await ReadUtf8Async(stream, name, result, budget, MaxFileBytes);
                    if (text is not null)
                        SplitText(text, name, fileName: name, result);
                }
                else if (ext == ".zip")
                {
                    await ReadZipAsync(file, name, result, budget);
                }
                else
                {
                    result.Errors.Add($"File \"{name}\" không phải .txt hoặc .zip. Hãy bỏ file này ra.");
                }
            }

        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException or NotSupportedException)
        {
            result.Errors.Add(ex is InvalidDataException ? ex.Message : "Không đọc được file tải lên.");
        }
        return result;
    }

    // Đọc từng file .txt trong zip
    private static async Task ReadZipAsync(IFormFile file, string zipName, ChapterReadResult result, ReadBudget budget)
    {
        using var stream = new MemoryStream();
        await using (var upload = file.OpenReadStream())
            await upload.CopyToAsync(stream);
        stream.Position = 0;

        ZipArchive zip;
        try
        {
            zip = new ZipArchive(stream, ZipArchiveMode.Read);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or IOException)
        {
            result.Errors.Add($"File \"{zipName}\" bị hỏng hoặc không phải file zip thật.");
            return;
        }

        using (zip)
        {
            // Bỏ thư mục, file ẩn, rác macOS
            var entries = zip.Entries
                .Where(e => !string.IsNullOrEmpty(e.Name)
                            && !e.FullName.StartsWith("__MACOSX", StringComparison.OrdinalIgnoreCase)
                            && !e.Name.StartsWith('.'))
                .OrderBy(e => e.FullName, NaturalComparer.Instance)
                .ToList();

            if (entries.Count > MaxZipEntries)
            {
                result.Errors.Add($"File \"{zipName}\" có hơn {MaxZipEntries} file bên trong.");
                return;
            }

            var notTxt = new List<string>();
            var txtCount = 0;

            foreach (var entry in entries)
            {
                var label = $"{zipName}/{entry.FullName}";

                if (!entry.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    notTxt.Add(entry.FullName);
                    continue;
                }
                if (entry.Length > MaxZipEntryBytes)
                {
                    result.Errors.Add($"\"{label}\" lớn hơn 2MB.");
                    continue;
                }

                txtCount++;
                await using var entryStream = entry.Open();
                var text = await ReadUtf8Async(entryStream, label, result, budget, MaxZipEntryBytes);
                if (text is not null)
                    SplitText(text, label, fileName: entry.Name, result);
            }

            if (notTxt.Count > 0)
                result.Warnings.Add($"Trong \"{zipName}\" có {notTxt.Count} file không phải .txt, đã bỏ qua: {Shorten(notTxt)}.");

            if (txtCount == 0)
                result.Errors.Add($"File \"{zipName}\" không có file .txt nào bên trong.");
        }
    }

    // Đọc chữ, bắt buộc UTF-8
    private static async Task<string?> ReadUtf8Async(Stream stream, string label, ChapterReadResult result, ReadBudget budget, long fileLimit)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(buffer)) > 0)
        {
            if (memory.Length + count > fileLimit)
                throw new InvalidDataException($"File {label} vượt giới hạn dung lượng giải nén.");
            budget.Add(count);
            await memory.WriteAsync(buffer.AsMemory(0, count));
        }
        var bytes = memory.ToArray();

        // Bỏ BOM
        var start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;

        try
        {
            return StrictUtf8.GetString(bytes, start, bytes.Length - start);
        }
        catch (DecoderFallbackException)
        {
            result.Errors.Add($"\"{label}\" bị lỗi font (không phải UTF-8). " +
                              "Mở file bằng Notepad → File → Save As → mục Encoding chọn UTF-8 → lưu lại rồi tải lên lại.");
            return null;
        }
    }

    // Tách văn bản thành các chương. fileName = null nghĩa là văn bản dán.
    private static void SplitText(string text, string source, string? fileName, ChapterReadResult result)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        var found = new List<ParsedChapter>();
        ParsedChapter? current = null;
        var currentLines = new List<string>();
        var textBeforeFirstHeading = new StringBuilder();
        var lookalikeLines = new List<string>();

        foreach (var line in lines)
        {
            var match = HeadingRegex.Match(line);
            if (!match.Success && LooksLikeHeadingRegex.IsMatch(line))
                lookalikeLines.Add(line.Trim());

            if (match.Success)
            {
                // Gặp tiêu đề → đóng chương cũ, mở chương mới
                CloseChapter(current, currentLines, found);

                if (result.Chapters.Count + found.Count >= MaxParsedChapters)
                    throw new InvalidDataException("Mỗi lần chỉ tải tối đa 500 chương.");
                current = new ParsedChapter
                {
                    Title = match.Groups["title"].Value.Trim(),
                    Source = source,
                    NumberFrom = "Dòng tiêu đề"
                };
                if (int.TryParse(match.Groups["num"].Value, out var number) && number >= 1)
                {
                    current.ChapterNumber = number;
                }
                else
                {
                    current.HasError = true;
                    current.Problems.Add($"Số chương \"{match.Groups["num"].Value}\" không hợp lệ.");
                }
                currentLines = new List<string>();
            }
            else if (current is null)
            {
                textBeforeFirstHeading.AppendLine(line);
            }
            else
            {
                currentLines.Add(line);
            }
        }
        CloseChapter(current, currentLines, found);

        foreach (var l in lookalikeLines.Take(5))
            result.Warnings.Add($"\"{source}\": dòng \"{Cut(l, 60)}\" bắt đầu bằng \"Chương + số\" nhưng sau số là chữ thường "
                              + "nên được coi là CÂU VĂN, không tách thành chương. Nếu đây là tiêu đề, hãy thêm dấu \":\" (vd: \"Chương 5: ...\").");
        if (lookalikeLines.Count > 5)
            result.Warnings.Add($"\"{source}\": còn {lookalikeLines.Count - 5} dòng tương tự khác.");

        // A. Có dòng "Chương N"
        if (found.Count > 0)
        {
            if (!string.IsNullOrWhiteSpace(textBeforeFirstHeading.ToString()))
                result.Warnings.Add($"\"{source}\": có đoạn chữ nằm trước dòng \"Chương ...\" đầu tiên, không thuộc chương nào nên đã bỏ qua.");

            result.Chapters.AddRange(found);
            return;
        }

        // B. Văn bản dán không có dòng "Chương N"
        if (fileName is null)
        {
            result.Errors.Add("Văn bản dán không có dòng nào dạng \"Chương 1: Tên chương\", nên không biết chỗ nào bắt đầu chương.");
            return;
        }

        // C. File không có dòng "Chương N" → cả file là 1 chương, số lấy từ tên file
        var nameOnly = Path.GetFileNameWithoutExtension(fileName);
        var chapter = new ParsedChapter
        {
            Content = text.Replace("\r\n", "\n").Trim(),
            Source = source
        };

        var numbers = DigitsRegex.Matches(nameOnly);
        if (numbers.Count > 0 && int.TryParse(numbers[^1].Value, out var fromName) && fromName >= 1)
        {
            chapter.ChapterNumber = fromName;   // lấy số cuối: "truyen2_chuong15" → 15
            chapter.NumberFrom = "Tên file";
            var rest = FileNamePrefixRegex.Replace(nameOnly, "").Trim();
            chapter.Title = rest.Any(char.IsLetter) ? rest : string.Empty;
        }
        else
        {
            chapter.Title = nameOnly.Trim();    // không có số → ChapterService tự đánh số
        }

        if (result.Chapters.Count >= MaxParsedChapters)
            throw new InvalidDataException("Mỗi lần chỉ tải tối đa 500 chương.");
        result.Chapters.Add(chapter);
    }

    private static void CloseChapter(ParsedChapter? chapter, List<string> lines, List<ParsedChapter> found)
    {
        if (chapter is null)
            return;
        chapter.Content = string.Join("\n", lines).Trim();
        found.Add(chapter);
    }

    private static string Cut(string text, int max)
        => text.Length <= max ? text : text[..max] + "…";

    private static string Shorten(List<string> items, int max = 5)
        => items.Count <= max
            ? string.Join(", ", items)
            : $"{string.Join(", ", items.Take(max))} và {items.Count - max} file khác";

    // So sánh tên file kiểu số tự nhiên: 1, 2, 10 (thay vì 1, 10, 2)
    private sealed class NaturalComparer : IComparer<string>
    {
        public static readonly NaturalComparer Instance = new();
        private static readonly Regex Parts = new(@"\d+|\D+", RegexOptions.Compiled);

        public int Compare(string? x, string? y)
        {
            var a = Parts.Matches(x ?? string.Empty);
            var b = Parts.Matches(y ?? string.Empty);
            for (var i = 0; i < Math.Min(a.Count, b.Count); i++)
            {
                var pa = a[i].Value;
                var pb = b[i].Value;
                int cmp;
                if (char.IsDigit(pa[0]) && char.IsDigit(pb[0]))
                {
                    var ta = pa.TrimStart('0');
                    var tb = pb.TrimStart('0');
                    cmp = ta.Length != tb.Length ? ta.Length.CompareTo(tb.Length) : string.CompareOrdinal(ta, tb);
                }
                else
                {
                    cmp = string.Compare(pa, pb, StringComparison.OrdinalIgnoreCase);
                }
                if (cmp != 0)
                    return cmp;
            }
            return a.Count.CompareTo(b.Count);
        }
    }
}