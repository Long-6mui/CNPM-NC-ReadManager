namespace ReadManager.Api.Entities;

// PB33 — BÁO CÁO VI PHẠM do độc giả gửi.
// TargetType + TargetId cho biết báo cáo cái gì: "Story" | "Chapter" | "Comment" + Id tương ứng.
// Status: "Pending" (chờ xử lý) | "Resolved" (đã xử lý) | "Rejected" (bác bỏ)
public class Report
{
    public int ReportId { get; set; }
    public int ReporterId { get; set; }      // người gửi báo cáo
    public string TargetType { get; set; } = string.Empty;
    public int TargetId { get; set; }
    public string Reason { get; set; } = string.Empty;   // lý do chọn sẵn: Spam, Nội dung xấu...
    public string? Note { get; set; }                    // ghi chú thêm (không bắt buộc)
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? HandledAt { get; set; }             // lúc admin xử lý

    public User Reporter { get; set; } = null!;
}