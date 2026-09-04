using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services.DocumentControl;

public class DocumentConfigurationService : IDocumentConfigurationService
{
    private readonly MicroLimsDbContext _db;
    private readonly IAuditEventService _auditEventService;
    private readonly IDocumentAuthorizationService _authService;

    public DocumentConfigurationService(
        MicroLimsDbContext db,
        IAuditEventService auditEventService,
        IDocumentAuthorizationService authService)
    {
        _db = db;
        _auditEventService = auditEventService;
        _authService = authService;
    }

    private async Task EnsureAdminAsync(int userId)
    {
        var canManage = await _authService.CanManageConfigurationAsync(userId);
        if (!canManage)
            throw new UnauthorizedAccessException("Only System Administrators can manage module configuration.");
    }

    // ---- Document Types ----

    public async Task<List<DocumentTypeDto>> GetDocumentTypesAsync(bool includeInactive = false)
    {
        var query = _db.DocumentTypes.AsNoTracking();
        if (!includeInactive)
            query = query.Where(t => t.IsActive);

        return await query
            .OrderBy(t => t.Name)
            .Select(t => new DocumentTypeDto(t.Id, t.Code, t.Name, t.DefaultReviewCycleMonths, t.IsActive))
            .ToListAsync();
    }

    public async Task<DocumentTypeDto> CreateDocumentTypeAsync(CreateDocumentTypeRequest request, int userId)
    {
        await EnsureAdminAsync(userId);

        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ArgumentException("Document Type Code is required.", nameof(request.Code));

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Document Type Name is required.", nameof(request.Name));

        if (request.DefaultReviewCycleMonths <= 0 || request.DefaultReviewCycleMonths > 120)
            throw new ArgumentException("Default review cycle must be between 1 and 120 months.", nameof(request.DefaultReviewCycleMonths));

        var trimmedCode = request.Code.Trim().ToUpperInvariant();
        if (await _db.DocumentTypes.AnyAsync(t => t.Code == trimmedCode))
            throw new InvalidOperationException($"Document Type Code '{trimmedCode}' already exists.");

        var docType = new DocumentType
        {
            Code = trimmedCode,
            Name = request.Name.Trim(),
            DefaultReviewCycleMonths = request.DefaultReviewCycleMonths,
            IsActive = true
        };

        _db.DocumentTypes.Add(docType);
        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        var changes = new List<AuditFieldChange>
        {
            new("Code", null, docType.Code),
            new("Name", null, docType.Name),
            new("DefaultReviewCycleMonths", null, docType.DefaultReviewCycleMonths.ToString())
        };

        await _auditEventService.RecordUserEventAsync(
            actionCode: "DocumentTypeCreated",
            actionCategory: AuditActionCategory.Configuration,
            recordType: nameof(DocumentType),
            changes: changes,
            entityId: docType.Id.ToString());

        return new DocumentTypeDto(docType.Id, docType.Code, docType.Name, docType.DefaultReviewCycleMonths, docType.IsActive);
    }

    public async Task<DocumentTypeDto> UpdateDocumentTypeAsync(int id, UpdateDocumentTypeRequest request, int userId)
    {
        await EnsureAdminAsync(userId);

        var docType = await _db.DocumentTypes.FindAsync(id)
            ?? throw new KeyNotFoundException($"Document Type {id} not found.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Document Type Name is required.", nameof(request.Name));

        if (request.DefaultReviewCycleMonths <= 0 || request.DefaultReviewCycleMonths > 120)
            throw new ArgumentException("Default review cycle must be between 1 and 120 months.", nameof(request.DefaultReviewCycleMonths));

        var changes = new List<AuditFieldChange>();
        if (docType.Name != request.Name.Trim())
        {
            changes.Add(new("Name", docType.Name, request.Name.Trim()));
            docType.Name = request.Name.Trim();
        }

        if (docType.DefaultReviewCycleMonths != request.DefaultReviewCycleMonths)
        {
            changes.Add(new("DefaultReviewCycleMonths", docType.DefaultReviewCycleMonths.ToString(), request.DefaultReviewCycleMonths.ToString()));
            docType.DefaultReviewCycleMonths = request.DefaultReviewCycleMonths;
        }

        if (docType.IsActive != request.IsActive)
        {
            changes.Add(new("IsActive", docType.IsActive.ToString(), request.IsActive.ToString()));
            docType.IsActive = request.IsActive;
        }

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        if (changes.Count > 0)
        {
            await _auditEventService.RecordUserEventAsync(
                actionCode: "DocumentTypeUpdated",
                actionCategory: AuditActionCategory.Configuration,
                recordType: nameof(DocumentType),
                changes: changes,
                entityId: docType.Id.ToString());
        }

        return new DocumentTypeDto(docType.Id, docType.Code, docType.Name, docType.DefaultReviewCycleMonths, docType.IsActive);
    }

    // ---- Departments & Sections ----

    public async Task<List<DocumentDepartmentDto>> GetDepartmentsAsync(bool includeInactive = false)
    {
        var query = _db.DocumentDepartments
            .Include(d => d.Sections)
            .AsNoTracking();

        if (!includeInactive)
            query = query.Where(d => d.IsActive);

        return await query
            .OrderBy(d => d.Name)
            .Select(d => new DocumentDepartmentDto(
                d.Id,
                d.Code,
                d.Name,
                d.IsActive,
                d.Sections.Where(s => includeInactive || s.IsActive)
                    .Select(s => new DocumentSectionDto(s.Id, s.DepartmentId, d.Name, s.Name, s.IsActive))
                    .ToList()))
            .ToListAsync();
    }

    public async Task<DocumentDepartmentDto> CreateDepartmentAsync(CreateDocumentDepartmentRequest request, int userId)
    {
        await EnsureAdminAsync(userId);

        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ArgumentException("Department Code is required.", nameof(request.Code));

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Department Name is required.", nameof(request.Name));

        var trimmedCode = request.Code.Trim().ToUpperInvariant();
        if (await _db.DocumentDepartments.AnyAsync(d => d.Code == trimmedCode))
            throw new InvalidOperationException($"Department Code '{trimmedCode}' already exists.");

        var dept = new DocumentDepartment
        {
            Code = trimmedCode,
            Name = request.Name.Trim(),
            IsActive = true
        };

        _db.DocumentDepartments.Add(dept);
        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        var changes = new List<AuditFieldChange>
        {
            new("Code", null, dept.Code),
            new("Name", null, dept.Name)
        };

        await _auditEventService.RecordUserEventAsync(
            actionCode: "DocumentDepartmentCreated",
            actionCategory: AuditActionCategory.Configuration,
            recordType: nameof(DocumentDepartment),
            changes: changes,
            entityId: dept.Id.ToString());

        return new DocumentDepartmentDto(dept.Id, dept.Code, dept.Name, dept.IsActive, new List<DocumentSectionDto>());
    }

    public async Task<DocumentDepartmentDto> UpdateDepartmentAsync(int id, UpdateDocumentDepartmentRequest request, int userId)
    {
        await EnsureAdminAsync(userId);

        var dept = await _db.DocumentDepartments
            .Include(d => d.Sections)
            .FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new KeyNotFoundException($"Department {id} not found.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Department Name is required.", nameof(request.Name));

        var changes = new List<AuditFieldChange>();
        if (dept.Name != request.Name.Trim())
        {
            changes.Add(new("Name", dept.Name, request.Name.Trim()));
            dept.Name = request.Name.Trim();
        }

        if (dept.IsActive != request.IsActive)
        {
            changes.Add(new("IsActive", dept.IsActive.ToString(), request.IsActive.ToString()));
            dept.IsActive = request.IsActive;
        }

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        if (changes.Count > 0)
        {
            await _auditEventService.RecordUserEventAsync(
                actionCode: "DocumentDepartmentUpdated",
                actionCategory: AuditActionCategory.Configuration,
                recordType: nameof(DocumentDepartment),
                changes: changes,
                entityId: dept.Id.ToString());
        }

        return new DocumentDepartmentDto(
            dept.Id,
            dept.Code,
            dept.Name,
            dept.IsActive,
            dept.Sections.Select(s => new DocumentSectionDto(s.Id, s.DepartmentId, dept.Name, s.Name, s.IsActive)).ToList());
    }

    public async Task<List<DocumentSectionDto>> GetSectionsByDepartmentAsync(int departmentId, bool includeInactive = false)
    {
        var dept = await _db.DocumentDepartments.FindAsync(departmentId)
            ?? throw new KeyNotFoundException($"Department {departmentId} not found.");

        var query = _db.DocumentSections
            .Where(s => s.DepartmentId == departmentId)
            .AsNoTracking();

        if (!includeInactive)
            query = query.Where(s => s.IsActive);

        return await query
            .OrderBy(s => s.Name)
            .Select(s => new DocumentSectionDto(s.Id, s.DepartmentId, dept.Name, s.Name, s.IsActive))
            .ToListAsync();
    }

    public async Task<DocumentSectionDto> CreateSectionAsync(CreateDocumentSectionRequest request, int userId)
    {
        await EnsureAdminAsync(userId);

        var dept = await _db.DocumentDepartments.FindAsync(request.DepartmentId)
            ?? throw new KeyNotFoundException($"Department {request.DepartmentId} not found.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Section Name is required.", nameof(request.Name));

        var trimmedName = request.Name.Trim();
        if (await _db.DocumentSections.AnyAsync(s => s.DepartmentId == request.DepartmentId && s.Name.ToLower() == trimmedName.ToLower()))
            throw new InvalidOperationException($"Section '{trimmedName}' already exists under department '{dept.Name}'.");

        var section = new DocumentSection
        {
            DepartmentId = request.DepartmentId,
            Name = trimmedName,
            IsActive = true
        };

        _db.DocumentSections.Add(section);
        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        var changes = new List<AuditFieldChange>
        {
            new("DepartmentId", null, request.DepartmentId.ToString()),
            new("Name", null, section.Name)
        };

        await _auditEventService.RecordUserEventAsync(
            actionCode: "DocumentSectionCreated",
            actionCategory: AuditActionCategory.Configuration,
            recordType: nameof(DocumentSection),
            changes: changes,
            entityId: section.Id.ToString());

        return new DocumentSectionDto(section.Id, section.DepartmentId, dept.Name, section.Name, section.IsActive);
    }

    public async Task<DocumentSectionDto> UpdateSectionAsync(int id, UpdateDocumentSectionRequest request, int userId)
    {
        await EnsureAdminAsync(userId);

        var section = await _db.DocumentSections
            .Include(s => s.Department)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new KeyNotFoundException($"Section {id} not found.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Section Name is required.", nameof(request.Name));

        var changes = new List<AuditFieldChange>();
        if (section.Name != request.Name.Trim())
        {
            changes.Add(new("Name", section.Name, request.Name.Trim()));
            section.Name = request.Name.Trim();
        }

        if (section.IsActive != request.IsActive)
        {
            changes.Add(new("IsActive", section.IsActive.ToString(), request.IsActive.ToString()));
            section.IsActive = request.IsActive;
        }

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        if (changes.Count > 0)
        {
            await _auditEventService.RecordUserEventAsync(
                actionCode: "DocumentSectionUpdated",
                actionCategory: AuditActionCategory.Configuration,
                recordType: nameof(DocumentSection),
                changes: changes,
                entityId: section.Id.ToString());
        }

        return new DocumentSectionDto(section.Id, section.DepartmentId, section.Department?.Name ?? "", section.Name, section.IsActive);
    }

    // ---- Numbering Configuration ----

    public async Task<DocumentNumberingConfigDto> GetNumberingConfigAsync()
    {
        var config = await _db.DocumentNumberingConfigurations
            .Include(c => c.ModifiedByUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IsEnabled)
            ?? new DocumentNumberingConfiguration
            {
                Prefix = "DOC-",
                NumberFormat = "0000000",
                IsEnabled = true
            };

        var sample = $"{config.Prefix}{(1).ToString(config.NumberFormat)}";
        return new DocumentNumberingConfigDto(
            config.Id,
            config.Prefix,
            config.NumberFormat,
            config.IsEnabled,
            config.ModifiedAt,
            config.ModifiedByUserId,
            config.ModifiedByUser?.FullName ?? "System",
            sample
        );
    }

    public async Task<DocumentNumberingConfigDto> UpdateNumberingConfigAsync(UpdateDocumentNumberingConfigRequest request, int userId)
    {
        await EnsureAdminAsync(userId);

        if (string.IsNullOrWhiteSpace(request.Prefix))
            throw new ArgumentException("Prefix is required.", nameof(request.Prefix));

        if (string.IsNullOrWhiteSpace(request.NumberFormat))
            throw new ArgumentException("Number Format is required (e.g. 0000000).", nameof(request.NumberFormat));

        var config = await _db.DocumentNumberingConfigurations.FirstOrDefaultAsync();
        var now = DateTime.UtcNow;

        _db.CurrentUserId = userId;

        if (config == null)
        {
            config = new DocumentNumberingConfiguration
            {
                Prefix = request.Prefix.Trim(),
                NumberFormat = request.NumberFormat.Trim(),
                IsEnabled = request.IsEnabled,
                ModifiedAt = now,
                ModifiedByUserId = userId
            };
            _db.DocumentNumberingConfigurations.Add(config);
            await _db.SaveChangesAsync();

            await _auditEventService.RecordUserEventAsync(
                actionCode: "DocumentNumberingConfigUpdated",
                actionCategory: AuditActionCategory.Configuration,
                recordType: nameof(DocumentNumberingConfiguration),
                changes: new List<AuditFieldChange>
                {
                    new("Prefix", null, config.Prefix),
                    new("NumberFormat", null, config.NumberFormat),
                    new("IsEnabled", null, config.IsEnabled.ToString())
                },
                entityId: config.Id.ToString());
        }
        else
        {
            var changes = new List<AuditFieldChange>();
            if (config.Prefix != request.Prefix.Trim())
            {
                changes.Add(new("Prefix", config.Prefix, request.Prefix.Trim()));
                config.Prefix = request.Prefix.Trim();
            }

            if (config.NumberFormat != request.NumberFormat.Trim())
            {
                changes.Add(new("NumberFormat", config.NumberFormat, request.NumberFormat.Trim()));
                config.NumberFormat = request.NumberFormat.Trim();
            }

            if (config.IsEnabled != request.IsEnabled)
            {
                changes.Add(new("IsEnabled", config.IsEnabled.ToString(), request.IsEnabled.ToString()));
                config.IsEnabled = request.IsEnabled;
            }

            config.ModifiedAt = now;
            config.ModifiedByUserId = userId;

            if (changes.Count > 0)
            {
                await _auditEventService.RecordUserEventAsync(
                    actionCode: "DocumentNumberingConfigUpdated",
                    actionCategory: AuditActionCategory.Configuration,
                    recordType: nameof(DocumentNumberingConfiguration),
                    changes: changes,
                    entityId: config.Id.ToString());
            }

            await _db.SaveChangesAsync();
        }

        var sample = $"{config.Prefix}{(1).ToString(config.NumberFormat)}";
        var user = await _db.Users.FindAsync(userId);
        return new DocumentNumberingConfigDto(
            config.Id,
            config.Prefix,
            config.NumberFormat,
            config.IsEnabled,
            config.ModifiedAt,
            config.ModifiedByUserId,
            user?.FullName ?? "Admin",
            sample
        );
    }

    // ---- Configuration Settings ----

    public async Task<List<ConfigurationSettingDto>> GetConfigurationSettingsAsync()
    {
        return await _db.ConfigurationSettings
            .Include(s => s.ModifiedByUser)
            .AsNoTracking()
            .OrderBy(s => s.SettingGroup).ThenBy(s => s.SettingKey)
            .Select(s => new ConfigurationSettingDto(
                s.Id,
                s.SettingKey,
                s.SettingValue,
                s.DataType,
                s.SettingGroup,
                s.ModifiedAt,
                s.ModifiedByUserId,
                s.ModifiedByUser != null ? s.ModifiedByUser.FullName : null
            ))
            .ToListAsync();
    }

    public async Task<ConfigurationSettingDto> UpdateConfigurationSettingAsync(string key, UpdateConfigurationSettingRequest request, int userId)
    {
        await EnsureAdminAsync(userId);

        if (request.SettingValue == null)
            throw new ArgumentException("Setting value cannot be null.", nameof(request.SettingValue));

        var setting = await _db.ConfigurationSettings
            .Include(s => s.ModifiedByUser)
            .FirstOrDefaultAsync(s => s.SettingKey == key);

        string? prevVal = null;
        if (setting == null)
        {
            setting = new ConfigurationSetting
            {
                SettingKey = key,
                SettingValue = request.SettingValue.Trim(),
                DataType = "string",
                SettingGroup = "DocumentControl",
                ModifiedAt = DateTime.UtcNow,
                ModifiedByUserId = userId
            };
            _db.ConfigurationSettings.Add(setting);
        }
        else
        {
            prevVal = setting.SettingValue;
            setting.SettingValue = request.SettingValue.Trim();
            setting.ModifiedAt = DateTime.UtcNow;
            setting.ModifiedByUserId = userId;
        }

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        var changes = new List<AuditFieldChange>
        {
            new("SettingValue", prevVal, setting.SettingValue)
        };

        await _auditEventService.RecordUserEventAsync(
            actionCode: "ConfigurationSettingUpdated",
            actionCategory: AuditActionCategory.Configuration,
            recordType: nameof(ConfigurationSetting),
            reason: $"Setting '{key}' updated",
            changes: changes,
            entityId: setting.Id.ToString());

        var user = await _db.Users.FindAsync(userId);
        return new ConfigurationSettingDto(
            setting.Id,
            setting.SettingKey,
            setting.SettingValue,
            setting.DataType,
            setting.SettingGroup,
            setting.ModifiedAt,
            setting.ModifiedByUserId,
            user?.FullName
        );
    }
}
