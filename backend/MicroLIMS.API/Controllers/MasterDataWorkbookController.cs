using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.MasterDataWorkbook;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

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

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost("validate")]
    [RequestSizeLimit(MasterDataWorkbookService.MaxFileBytes + 1024 * 1024)]
    public async Task<IActionResult> Validate(IFormFile file) =>
        Ok(ApiResponse<object>.Ok(await _service.ValidateAsync(await ReadAsync(file), CurrentUserId)));

    private static async Task<byte[]> ReadAsync(IFormFile file)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        return ms.ToArray();
    }

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost("import")]
    [RequestSizeLimit(MasterDataWorkbookService.MaxFileBytes + 1024 * 1024)]
    public async Task<IActionResult> Import(IFormFile file, [FromForm] string password, [FromForm] string? comment) =>
        Ok(ApiResponse<object>.Ok(await _service.ImportAsync(await ReadAsync(file), file.FileName, CurrentUserId, password, comment, IpAddress)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpGet("imports")]
    public async Task<IActionResult> GetImports() =>
        Ok(ApiResponse<object>.Ok(await _service.GetImportsAsync()));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost("imports/{id}/verify")]
    public async Task<IActionResult> Verify(int id, VerifyMasterDataImportRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.VerifyAsync(id, CurrentUserId, request.Password, request.Comment, IpAddress)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpGet("imports/{id}/file")]
    public async Task<IActionResult> GetFile(int id)
    {
        var (content, fileName) = await _service.GetFileAsync(id);
        return File(content, MasterDataWorkbookService.XlsxContentType, fileName);
    }
}
