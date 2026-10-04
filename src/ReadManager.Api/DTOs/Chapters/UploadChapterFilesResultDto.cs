namespace ReadManager.Api.DTOs.Chapters;

// KẾT QUẢ KIỂM TRA / LƯU khi tải chương bằng file.
//   Errors   = lỗi chung, CHẶN không cho lưu (vd: file không phải .txt, file lỗi font, trùng số chương)
//   Warnings = cảnh báo, VẪN cho lưu (vd: thiếu chương 3, có đoạn chữ không thuộc chương nào)
public class UploadChapterFilesResultDto
{
    public bool CanSave { get; set; }   // true = không còn lỗi, bấm Lưu được
    public bool Saved { get; set; }     // true = đã lưu vào database

    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<ChapterPreviewItemDto> Chapters { get; set; } = new();

    // Chỉ có giá trị khi Saved = true
    public int Created { get; set; }
    public int Updated { get; set; }
    public List<int> SkippedNumbers { get; set; } = new();

    public string Message { get; set; } = string.Empty;   // Câu tóm tắt hiện trên màn hình
}