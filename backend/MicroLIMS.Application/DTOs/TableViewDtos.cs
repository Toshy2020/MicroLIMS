namespace MicroLIMS.Application.DTOs;

public sealed record TableViewDto(
    int Id, string TableKey, string Name, string Scope, int? SectionId, string? SectionName,
    string StateJson, bool IsMine, bool CanEdit, bool IsDefault, string OwnerName);
