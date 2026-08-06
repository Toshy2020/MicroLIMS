using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// The signature trail for a record - what an auditor asks to see.
// Read-only: there is deliberately no POST/PUT/DELETE here or anywhere
// else for ElectronicSignature - see the entity's append-only comment.
[ApiController]
[Route("api/signatures")]
[Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
public class SignaturesController : ControllerBase
{
    private readonly MicroLimsDbContext _db;

    public SignaturesController(MicroLimsDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetTrail([FromQuery] string entityType, [FromQuery] int entityId)
    {
        var trail = await _db.ElectronicSignatures
            .Where(s => s.EntityType == entityType && s.EntityId == entityId)
            .OrderBy(s => s.SignedAt)
            .Select(s => new SignatureDto(s.UserFullNameSnapshot, s.UsernameSnapshot, s.RoleSnapshot, s.MeaningOfSignature.ToString(), s.SignedAt, s.Comment))
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(trail));
    }
}
