using ReadManager.Mobile.Models;

namespace ReadManager.Mobile.Services;

// Gói đọc đang bán (API: GET api/reading-plans, công khai)
public interface IPlanService
{
    Task<List<ReadingPlan>> GetPlansAsync();
}
