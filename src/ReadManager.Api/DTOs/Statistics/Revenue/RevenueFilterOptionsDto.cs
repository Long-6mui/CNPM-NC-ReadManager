namespace ReadManager.Api.DTOs.Statistics.Revenue;

// PB38 + PB39 — Danh sách để đổ vào ô chọn "gói" / "truyện": chỉ gồm gói và truyện TỪNG có đơn đã thanh toán
public class RevenueFilterOptionsDto
{
    public List<RevenuePlanOptionDto> Plans { get; set; } = new();
    public List<RevenueStoryOptionDto> Stories { get; set; } = new();
}

public class RevenuePlanOptionDto
{
    public int PlanVersionId { get; set; }
    public int PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public int DurationMonths { get; set; }
}

public class RevenueStoryOptionDto
{
    public int StoryId { get; set; }
    public string Title { get; set; } = string.Empty;
}
