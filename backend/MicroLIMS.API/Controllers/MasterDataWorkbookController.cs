using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.MasterDataWorkbook;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.API.Controllers;

// Master data workbook. Maps HTTP to MasterDataWorkbookService; the rules
// live in the Application layer.
[ApiController]
[Route("api/masterdata/workbook")]
[Authorize]
public class MasterDataWorkbookController : ControllerBase
{
    private readonly MasterDataWorkbookService _service;

    public MasterDataWorkbookController(MasterDataWorkbookService service) { _service = service; }

    private int CurrentUserId => int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;
    private string? IpAddress => HttpContext.Connection.RemoteIpAddress?.ToString();

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpGet]
    public async Task<IActionResult> Download()
    {
        var (content, fileName) = await _service.DownloadAsync();
        return File(content, MasterDataWorkbookService.XlsxContentType, fileName);
    }
}
