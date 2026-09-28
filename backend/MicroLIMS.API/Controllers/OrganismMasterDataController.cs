using MicroLIMS.Shared.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;
using MicroLIMS.Application.Services.MasterData;

namespace MicroLIMS.API.Controllers;

// Master data: organism. One of the controllers that share the
// api/masterdata route; the rules and data access live in the
// Application layer (OrganismMasterDataService).
[ApiController]
[Route("api/masterdata")]
[Authorize]
public class OrganismMasterDataController : ControllerBase
{
    private readonly OrganismMasterDataService _service;

    public OrganismMasterDataController(OrganismMasterDataService service)
    {
        _service = service;
    }

    // ---- Organisms (canonical master list backing every OrganismId FK -
    // MediaChallengeSpec, MediaEvaluationChallenge, Cryovial, Material) ----
    [HttpGet("organisms")]
    public async Task<IActionResult> GetOrganisms() =>
        Ok(ApiResponse<object>.Ok(await _service.GetOrganismsAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("organisms")]
    public async Task<IActionResult> CreateOrganism(CreateOrganismRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateOrganismAsync(request)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("organisms/{id}")]
    public async Task<IActionResult> UpdateOrganism(int id, UpdateOrganismRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateOrganismAsync(id, request)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("organisms/{id}")]
    public async Task<IActionResult> DeleteOrganism(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.DeleteOrganismAsync(id)));

}
