namespace ReadManager.Api.DTOs.Entitlements;

public class AccessStatusDto
{
    public int? StoryId { get; set; }
    public int? ChapterId { get; set; }
    public bool HasAccess { get; set; }
    public string? Reason { get; set; }
    public string? AccessType { get; set; }
}
