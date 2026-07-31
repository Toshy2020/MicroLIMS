using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize(Roles = RoleConstants.SystemAdministrator)]
public class RoleController : ControllerBase
{
    private readonly MicroLimsDbContext _db;

    public RoleController(MicroLimsDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(ApiResponse<List<Role>>.Ok(await _db.Roles.ToListAsync()));
}
