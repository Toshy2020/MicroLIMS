using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/pathogen-session")]
[Authorize(Roles = RoleConstants.Analyst + "," + RoleConstants.Reviewer + "," + RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
public class PathogenSessionController : ControllerBase
{
    private readonly PathogenSessionService _sessionService;

    public PathogenSessionController(PathogenSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("{sampleId:int}")]
    public async Task<IActionResult> GetSession(int sampleId)
    {
        var session = await _sessionService.GetSessionAsync(sampleId);
        if (session == null)
            return NotFound(ApiResponse<string>.Fail("Testing session / sample not found."));

        return Ok(ApiResponse<PathogenTestingSessionDto>.Ok(session));
    }

    [HttpPost("{sampleId:int}/start-tsb")]
    public async Task<IActionResult> StartSharedTsb(int sampleId, [FromBody] StartSharedTsbRequest request)
    {
        try
        {
            var res = await _sessionService.StartSharedTsbAsync(sampleId, request, CurrentUserId);
            return Ok(ApiResponse<SharedTsbStateDto>.Ok(res));
        }
        catch (WorkflowStepException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message, new List<string> { ex.ErrorCode, ex.Message }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    [HttpPost("{sampleId:int}/save-matrix")]
    public async Task<IActionResult> SaveResultMatrix(int sampleId, [FromBody] SaveResultMatrixRequest request)
    {
        try
        {
            var session = await _sessionService.SaveResultMatrixAsync(sampleId, request, CurrentUserId);
            return Ok(ApiResponse<PathogenTestingSessionDto>.Ok(session));
        }
        catch (WorkflowStepException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message, new List<string> { ex.ErrorCode, ex.Message }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    [HttpPost("{sampleId:int}/save-primary-observations")]
    public async Task<IActionResult> SavePrimaryObservations(int sampleId, [FromBody] SavePrimaryObservationsRequest request)
    {
        try
        {
            var session = await _sessionService.SavePrimaryObservationsAsync(sampleId, request, CurrentUserId);
            return Ok(ApiResponse<PathogenTestingSessionDto>.Ok(session));
        }
        catch (WorkflowStepException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message, new List<string> { ex.ErrorCode, ex.Message }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    [HttpGet("{sampleId:int}/eligible-confirmations")]
    public async Task<IActionResult> GetEligibleConfirmations(int sampleId, [FromQuery] int? testOrderId = null)
    {
        try
        {
            var eligible = await _sessionService.GetEligibleLocationsForConfirmationAsync(sampleId, testOrderId);
            return Ok(ApiResponse<List<EligibleLocationForConfirmationDto>>.Ok(eligible));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    [HttpPost("{sampleId:int}/start-confirmatory-setup")]
    public async Task<IActionResult> StartConfirmatorySetup(int sampleId, [FromBody] BatchConfirmatorySetupRequest request)
    {
        try
        {
            var session = await _sessionService.StartSharedConfirmatorySetupAsync(sampleId, request, CurrentUserId);
            return Ok(ApiResponse<PathogenTestingSessionDto>.Ok(session));
        }
        catch (WorkflowStepException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message, new List<string> { ex.ErrorCode, ex.Message }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    [HttpPost("{sampleId:int}/save-confirmatory-readings")]
    public async Task<IActionResult> SaveConfirmatoryReadings(int sampleId, [FromBody] SaveBatchConfirmatoryPlateReadingsRequest request)
    {
        try
        {
            var session = await _sessionService.SaveBatchConfirmatoryPlateReadingsAsync(sampleId, request, CurrentUserId);
            return Ok(ApiResponse<PathogenTestingSessionDto>.Ok(session));
        }
        catch (WorkflowStepException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message, new List<string> { ex.ErrorCode, ex.Message }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    [HttpPost("{sampleId:int}/complete")]
    public async Task<IActionResult> CompleteSession(int sampleId)
    {
        try
        {
            var session = await _sessionService.CompleteSessionAsync(sampleId, CurrentUserId);
            return Ok(ApiResponse<PathogenTestingSessionDto>.Ok(session));
        }
        catch (WorkflowStepException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message, new List<string> { ex.ErrorCode, ex.Message }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    [HttpPost("{sampleId:int}/reset")]
    public async Task<IActionResult> ResetSession(int sampleId, [FromBody] ResetPathogenSessionRequest? request)
    {
        try
        {
            var session = await _sessionService.ResetSessionAsync(sampleId, request?.Reason, CurrentUserId);
            return Ok(ApiResponse<PathogenTestingSessionDto>.Ok(session));
        }
        catch (WorkflowStepException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message, new List<string> { ex.ErrorCode, ex.Message }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }
}
