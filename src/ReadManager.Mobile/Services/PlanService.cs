using ReadManager.Mobile.Models;
using ReadManager.Mobile.Models.Common;

namespace ReadManager.Mobile.Services;

public class PlanService : IPlanService
{
    private readonly ApiClient _api;

    public PlanService(ApiClient api) => _api = api;

    public async Task<List<ReadingPlan>> GetPlansAsync()
    {
        var page = await _api.GetAsync<PagedResult<ReadingPlan>>("reading-plans?page=1&pageSize=100");
        return (page?.Items ?? new List<ReadingPlan>())
            .Where(p => p.CanBuy)
            .OrderBy(p => p.CurrentVersion!.Price)
            .ToList();
    }
}
