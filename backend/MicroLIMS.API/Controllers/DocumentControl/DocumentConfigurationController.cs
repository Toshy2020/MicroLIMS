using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers.DocumentControl;

[ApiController]
[Authorize]
[Route("api/document-control/config")]
public class DocumentConfigurationController : ControllerBase
{
    private readonly IDocumentConfigurationService _configService;

    public DocumentConfigurationController(IDocumentConfigurationService configService)
    {
        _configService = configService;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    // ---- Document Types ----

    [HttpGet("types")]
    public async Task<IActionResult> GetTypes([FromQuery] bool includeInactive = false)
    {
        var types = await _configService.GetDocumentTypesAsync(includeInactive);
        return Ok(ApiResponse<List<DocumentTypeDto>>.Ok(types));
    }

    [HttpPost("types")]
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> CreateType([FromBody] CreateDocumentTypeRequest request)
    {
        try
        {
            var result = await _configService.CreateDocumentTypeAsync(request, CurrentUserId);
            return Ok(ApiResponse<DocumentTypeDto>.Ok(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("types/{id:int}")]
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> UpdateType(int id, [FromBody] UpdateDocumentTypeRequest request)
    {
        try
        {
            var result = await _configService.UpdateDocumentTypeAsync(id, request, CurrentUserId);
            return Ok(ApiResponse<DocumentTypeDto>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // ---- Departments & Sections ----

    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments([FromQuery] bool includeInactive = false)
    {
        var depts = await _configService.GetDepartmentsAsync(includeInactive);
        return Ok(ApiResponse<List<DocumentDepartmentDto>>.Ok(depts));
    }

    [HttpPost("departments")]
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> CreateDepartment([FromBody] CreateDocumentDepartmentRequest request)
    {
        try
        {
            var result = await _configService.CreateDepartmentAsync(request, CurrentUserId);
            return Ok(ApiResponse<DocumentDepartmentDto>.Ok(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("departments/{id:int}")]
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> UpdateDepartment(int id, [FromBody] UpdateDocumentDepartmentRequest request)
    {
        try
        {
            var result = await _configService.UpdateDepartmentAsync(id, request, CurrentUserId);
            return Ok(ApiResponse<DocumentDepartmentDto>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("departments/{departmentId:int}/sections")]
    public async Task<IActionResult> GetSections(int departmentId, [FromQuery] bool includeInactive = false)
    {
        try
        {
            var sections = await _configService.GetSectionsByDepartmentAsync(departmentId, includeInactive);
            return Ok(ApiResponse<List<DocumentSectionDto>>.Ok(sections));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("sections")]
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> CreateSection([FromBody] CreateDocumentSectionRequest request)
    {
        try
        {
            var result = await _configService.CreateSectionAsync(request, CurrentUserId);
            return Ok(ApiResponse<DocumentSectionDto>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("sections/{id:int}")]
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> UpdateSection(int id, [FromBody] UpdateDocumentSectionRequest request)
    {
        try
        {
            var result = await _configService.UpdateSectionAsync(id, request, CurrentUserId);
            return Ok(ApiResponse<DocumentSectionDto>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // ---- Numbering Configuration ----

    [HttpGet("numbering")]
    public async Task<IActionResult> GetNumberingConfig()
    {
        var config = await _configService.GetNumberingConfigAsync();
        return Ok(ApiResponse<DocumentNumberingConfigDto>.Ok(config));
    }

    [HttpPut("numbering")]
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> UpdateNumberingConfig([FromBody] UpdateDocumentNumberingConfigRequest request)
    {
        try
        {
            var result = await _configService.UpdateNumberingConfigAsync(request, CurrentUserId);
            return Ok(ApiResponse<DocumentNumberingConfigDto>.Ok(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // ---- Settings ----

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var settings = await _configService.GetConfigurationSettingsAsync();
        return Ok(ApiResponse<List<ConfigurationSettingDto>>.Ok(settings));
    }

    [HttpPut("settings/{key}")]
    [Authorize(Roles = RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> UpdateSetting(string key, [FromBody] UpdateConfigurationSettingRequest request)
    {
        try
        {
            var result = await _configService.UpdateConfigurationSettingAsync(key, request, CurrentUserId);
            return Ok(ApiResponse<ConfigurationSettingDto>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
