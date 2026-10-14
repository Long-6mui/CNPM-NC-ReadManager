namespace ReadManager.Api.Entities;

public class ReadingPlan
{
    public int PlanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ReadingPlanVersion> Versions { get; set; }
        = new List<ReadingPlanVersion>();
}