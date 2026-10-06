namespace ReadManager.Api.DTOs.Chapters;

// KẾT QUẢ sau khi tải lên nhiều chương — API trả về cho Web hiện thông báo.
//  "Đã thêm 8 chương, cập nhật 0 chương, bỏ qua 2 chương đã có (số 1, 2)."

public class UploadChaptersResultDto
{
    public int Created { get; set; }                         // Số chương được thêm mới
    public int Updated { get; set; }                         // Số chương được 
    public List<int> SkippedNumbers { get; set; } = new();   // Các số chương bị bỏ qua
    public string Message { get; set; } = string.Empty;      // Câu thông báo tổng
}