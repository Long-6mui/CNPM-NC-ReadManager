namespace ReadManager.Api.Entities;

public class PlanVersionStory
{
    public int PlanVersionId { get; set; }
    public int StoryId { get; set; }

    public ReadingPlanVersion PlanVersion { get; set; } = null!;
    public Story Story { get; set; } = null!;
}