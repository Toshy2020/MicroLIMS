using MicroLIMS.Application.DTOs.DocumentControl;

namespace MicroLIMS.Application.Interfaces.DocumentControl;

public interface IDocumentConfigurationService
{
    // Document Types
    Task<List<DocumentTypeDto>> GetDocumentTypesAsync(bool includeInactive = false);
    Task<DocumentTypeDto> CreateDocumentTypeAsync(CreateDocumentTypeRequest request, int userId);
    Task<DocumentTypeDto> UpdateDocumentTypeAsync(int id, UpdateDocumentTypeRequest request, int userId);

    // Departments & Sections
    Task<List<DocumentDepartmentDto>> GetDepartmentsAsync(bool includeInactive = false);
    Task<DocumentDepartmentDto> CreateDepartmentAsync(CreateDocumentDepartmentRequest request, int userId);
    Task<DocumentDepartmentDto> UpdateDepartmentAsync(int id, UpdateDocumentDepartmentRequest request, int userId);

    Task<List<DocumentSectionDto>> GetSectionsByDepartmentAsync(int departmentId, bool includeInactive = false);
    Task<DocumentSectionDto> CreateSectionAsync(CreateDocumentSectionRequest request, int userId);
    Task<DocumentSectionDto> UpdateSectionAsync(int id, UpdateDocumentSectionRequest request, int userId);

    // Numbering Configuration
    Task<DocumentNumberingConfigDto> GetNumberingConfigAsync();
    Task<DocumentNumberingConfigDto> UpdateNumberingConfigAsync(UpdateDocumentNumberingConfigRequest request, int userId);

    // Settings
    Task<List<ConfigurationSettingDto>> GetConfigurationSettingsAsync();
    Task<ConfigurationSettingDto> UpdateConfigurationSettingAsync(string key, UpdateConfigurationSettingRequest request, int userId);
}
