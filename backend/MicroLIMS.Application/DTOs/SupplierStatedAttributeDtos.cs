namespace MicroLIMS.Application.DTOs;

public record SupplierStatedAttributeRequest(
    int ItemId = 0,
    string? Grade = null,
    string Name = "",
    string StatedValue = "",
    int SortOrder = 0);

public record SupplierStatedAttributeDto(
    int Id,
    int ItemId,
    string? Grade,
    string Name,
    string StatedValue,
    int SortOrder);
