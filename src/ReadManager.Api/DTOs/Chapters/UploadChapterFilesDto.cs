using System.ComponentModel.DataAnnotations;

namespace ReadManager.Api.DTOs.Chapters;

// PB08 — TẢI CHƯƠNG LÊN BẰNG FILE (.txt / .zip) HOẶC VĂN BẢN DÁN
// Gửi lên dạng form (multipart/form-data), không phải JSON, vì có file.
//
// Dùng 2 bước:
//   Save = false → "KIỂM TRA": API đọc file, tách chương, báo lỗi. CHƯA lưu gì.
//   Save = true  → "LƯU": kiểm tra lại y như trên, KHÔNG có lỗi thì mới lưu.

public class UploadChapterFilesDto
{
    // Các file được chọn: .txt (1 file chứa 1 hoặc nhiều chương) hoặc .zip (chứa nhiều file .txt)
    public List<IFormFile> Files { get; set; } = new();

    // Văn bản dán trực tiếp (không bắt buộc). Có thể vừa dán vừa chọn file.
    public string? Text { get; set; }

    // PB10 — áp dụng cho TẤT CẢ chương trong lần tải này
    [RegularExpression("^(Free|Paid)$", ErrorMessage = "AccessLevel phải là Free hoặc Paid.")]
    public string AccessLevel { get; set; } = "Free";

    [RegularExpression("^(Draft|Published)$", ErrorMessage = "PublicationStatus phải là Draft hoặc Published.")]
    public string PublicationStatus { get; set; } = "Published";

    // Hẹn giờ ra mắt cho TẤT CẢ chương lần này (giờ UTC). Để trống = ra mắt ngay.
    // Chỉ có tác dụng khi PublicationStatus = Published.
    public DateTime? ScheduledAt { get; set; }

    // Chương trùng số với chương đã có: true = ghi đè, false = bỏ qua
    public bool OverwriteExisting { get; set; }

    // false = chỉ kiểm tra (xem trước) | true = kiểm tra rồi lưu
    public bool Save { get; set; }
}