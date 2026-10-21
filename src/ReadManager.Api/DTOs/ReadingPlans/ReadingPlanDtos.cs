using System.ComponentModel.DataAnnotations;

namespace ReadManager.Api.DTOs.ReadingPlans;

public class CreatePlanVersionDto : IValidatableObject
{
    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal Price { get; set; }
    [Range(1, 120)]
    public int DurationMonths { get; set; }
    [Required, RegularExpression("^(All|Selected)$")]
    public string ScopeType { get; set; } = "Selected";
    [Required, MaxLength(1000)]
    public List<int> StoryIds { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (decimal.Round(Price, 2) != Price)
            yield return new("Giá tối đa hai chữ số thập phân.", new[] { nameof(Price) });
        if (StoryIds is null) yield break;
        if (StoryIds.Any(id => id <= 0) || StoryIds.Count != StoryIds.Distinct().Count())
            yield return new("Mã truyện phải dương và không trùng.", new[] { nameof(StoryIds) });
        if (ScopeType == "All" && StoryIds.Count != 0)
            yield return new("Gói All không nhận danh sách truyện.", new[] { nameof(StoryIds) });
        if (ScopeType == "Selected" && StoryIds.Count == 0)
            yield return new("Gói Selected phải có ít nhất một truyện.", new[] { nameof(StoryIds) });
    }
}

public class CreateReadingPlanDto : CreatePlanVersionDto
{
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;
}

public class RenameReadingPlanDto
{
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;
}

public class SetPlanAvailabilityDto
{
    [Required]
    public bool? IsAvailable { get; set; }
}

public record PlanVersionDto(int PlanVersionId, int VersionNumber, decimal Price,
    int DurationMonths, string ScopeType, DateTime CreatedAt, DateTime? PublishedAt, List<int> StoryIds);
public record ReadingPlanDto(int PlanId, string Name, bool IsAvailable, DateTime CreatedAt,
    PlanVersionDto? CurrentVersion);
public record PlanPageDto(List<ReadingPlanDto> Items, int TotalCount, int Page, int PageSize);
