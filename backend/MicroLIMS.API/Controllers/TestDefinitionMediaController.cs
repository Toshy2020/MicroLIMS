using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Generic "which MediaType(s) are approved for this test code" lookup
// (TestDefinitionMedia, StepName == null = the test's one unnamed
// step). Split out from the old CountTestController when Count Test/
// Pathogen moved to TestWorkflowEngine's own template-driven approval
// (TestWorkflowStep.MediaTypeId) - this endpoint's only remaining
// consumer is EMWorkflowEngine's incubation setup (EMIncubationDialog),
// which still uses the TestDefinitionMedia mechanism.
[ApiController]
[Route("api/test-definition-media-lookup")]
[Authorize]
public class TestDefinitionMediaController : ControllerBase
{
    private readonly MicroLimsDbContext _db;

    public TestDefinitionMediaController(MicroLimsDbContext db)
    {
        _db = db;
    }

    [HttpGet("approved-media")]
    public async Task<IActionResult> ApprovedMedia([FromQuery] string testCode)
    {
        var testDefinition = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Code == testCode);
        if (testDefinition is null) return Ok(ApiResponse<object>.Ok(new List<object>()));

        var mediaTypes = await _db.TestDefinitionMedias
            .Where(m => m.TestDefinitionId == testDefinition.Id && m.StepName == null)
            .Select(m => m.MediaType)
            .Distinct()
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(mediaTypes));
    }
}
