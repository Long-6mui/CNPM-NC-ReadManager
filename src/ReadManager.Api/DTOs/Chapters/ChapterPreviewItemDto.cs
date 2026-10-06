namespace ReadManager.Api.DTOs.Chapters;

// 1 DÒNG TRONG BẢNG XEM TRƯỚC — mỗi chương API tách được từ file/văn bản.
// Web hiện thành bảng; dòng có HasError = true tô đỏ, có Problems tô vàng.
public class ChapterPreviewItemDto
{
    public int ChapterNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public int ContentLength { get; set; }                        // Số ký tự nội dung
    public string ContentPreview { get; set; } = string.Empty;    // ~100 ký tự đầu để admin nhìn thử

    public string Source { get; set; } = string.Empty;       // Lấy từ đâu: "Văn bản dán", "truyen.txt", "truyen.zip/03.txt"
    public string NumberFrom { get; set; } = string.Empty;   // Số chương lấy từ: "Dòng tiêu đề" | "Tên file" | "Tự đánh số"

    // Sẽ làm gì với chương này khi bấm Lưu: "Thêm mới" | "Ghi đè" | "Bỏ qua" | "Lỗi"
    public string Action { get; set; } = string.Empty;

    public bool HasError { get; set; }                         // true = chặn không cho lưu
    public List<string> Problems { get; set; } = new();        // Các lỗi / cảnh báo của riêng chương này
}