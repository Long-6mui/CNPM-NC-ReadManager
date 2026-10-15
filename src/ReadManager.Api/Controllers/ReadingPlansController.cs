using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadManager.Api.DTOs.ReadingPlans;
using ReadManager.Api.Services;

namespace ReadManager.Api.Controllers;

[ApiController]
[Route("api/reading-plans")]
public class ReadingPlansController(IReadingPlanService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PlanPageDto>> List(int page = 1, int pageSize = 20)
        => Ok(await service.ListAsync(false, page, pageSize));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReadingPlanDto>> Get(int id)
    {
        var plan = await service.GetAsync(id, false);
        return plan is null ? NotFound() : Ok(plan);
    }

    [Authorize(Roles = "Admin"), HttpGet("admin")]
    public async Task<ActionResult<PlanPageDto>> AdminList(int page = 1, int pageSize = 20)
        => Ok(await service.ListAsync(true, page, pageSize));

    [Authorize(Roles = "Admin"), HttpGet("{id:int}/admin")]
    public async Task<ActionResult<ReadingPlanDto>> AdminGet(int id)
    {
        var plan = await service.GetAsync(id, true);
        return plan is null ? NotFound() : Ok(plan);
    }

    [Authorize(Roles = "Admin"), HttpGet("{id:int}/versions")]
    public async Task<ActionResult<List<PlanVersionDto>>> Versions(int id)
    {
        var versions = await service.VersionsAsync(id);
        return versions is null ? NotFound() : Ok(versions);
    }

    [Authorize(Roles = "Admin"), HttpPost]
    public async Task<ActionResult<ReadingPlanDto>> Create(CreateReadingPlanDto dto)
    {
        try
        {
            var plan = await service.CreateAsync(dto);
            return CreatedAtAction(nameof(AdminGet), new { id = plan.PlanId }, plan);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [Authorize(Roles = "Admin"), HttpPost("{id:int}/versions")]
    public async Task<ActionResult<PlanVersionDto>> AddVersion(int id, CreatePlanVersionDto dto)
    {
        try
        {
            var version = await service.AddVersionAsync(id, dto);
            return version is null ? NotFound() : CreatedAtAction(nameof(Versions), new { id }, version);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [Authorize(Roles = "Admin"), HttpPatch("{id:int}/name")]
    public async Task<IActionResult> Rename(int id, RenameReadingPlanDto dto)
        => await service.RenameAsync(id, dto) ? NoContent() : NotFound();

    [Authorize(Roles = "Admin"), HttpPatch("{id:int}/availability")]
    public async Task<IActionResult> Availability(int id, SetPlanAvailabilityDto dto)
        => await service.SetAvailabilityAsync(id, dto.IsAvailable!.Value) ? NoContent() : NotFound();
}
