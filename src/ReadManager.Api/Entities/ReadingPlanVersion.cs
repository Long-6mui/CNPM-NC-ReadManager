namespace ReadManager.Api.Entities;

public class ReadingPlanVersion
{
    public int PlanVersionId { get; set; }
    public int PlanId { get; set; }
    public int VersionNumber { get; set; }

    public decimal Price { get; set; }
    public int DurationMonths { get; set; }

    // All: toàn bộ truyện; Selected: các truyện được chọn.
    public string ScopeType { get; set; } = "Selected";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }

    public ReadingPlan Plan { get; set; } = null!;

    public ICollection<PlanVersionStory> PlanVersionStories { get; set; }
        = new List<PlanVersionStory>();
}