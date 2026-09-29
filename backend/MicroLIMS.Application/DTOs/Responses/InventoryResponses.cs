using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.Responses;

// Response types for items, materials and equipment inventory.

public class ItemResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public SampleCategory Category { get; init; }
    public string SopNumber { get; init; } = null!;
    public List<SpecificationResponse> Specifications { get; init; } = new();
    public List<SampleTestResponse> AssignedTests { get; init; } = new();
    public bool IsActive { get; init; }

    public static ItemResponse From(Item e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        Name = e.Name,
        Code = e.Code,
        Category = e.Category,
        SopNumber = e.SopNumber,
        Specifications = e.Specifications.Select(SpecificationResponse.From).ToList(),
        AssignedTests = e.AssignedTests.Select(SampleTestResponse.From).ToList(),
        IsActive = e.IsActive,
    };
}

public class SampleTestResponse
{
    public int Id { get; init; }
    public int ItemId { get; init; }
    public string TestCode { get; init; } = null!;
    public string DisplayName { get; init; } = null!;

    public static SampleTestResponse From(SampleTest e) => new()
    {
        Id = e.Id,
        ItemId = e.ItemId,
        TestCode = e.TestCode,
        DisplayName = e.DisplayName,
    };
}

public class MaterialResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public int SectionId { get; init; }
    public DocumentSectionResponse? Section { get; init; }
    public MaterialType MaterialType { get; init; }
    public string? CustomType { get; init; }
    public string MaterialName { get; init; } = null!;
    public string ManufacturerName { get; init; } = null!;
    public string BatchNumber { get; init; } = null!;
    public DateTime ReceivingDate { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public string? Code { get; init; }
    public string Location { get; init; } = null!;
    public int? MediaProductId { get; init; }
    public MediaProductResponse? MediaProduct { get; init; }
    public string? AtccNumber { get; init; }
    public int? OrganismId { get; init; }
    public OrganismResponse? Organism { get; init; }
    public decimal QuantityReceived { get; init; }
    public decimal QuantityRemaining { get; init; }
    public MaterialUnit Unit { get; init; }
    public decimal? MinimumStockLevel { get; init; }
    public decimal? Purity { get; init; }
    public int CreatedByUserId { get; init; }
    public DateTime CreatedAt { get; init; }
    public int LastModifiedByUserId { get; init; }
    public DateTime LastModifiedAt { get; init; }
    public StockStatus Status { get; init; }
    public bool IsUsable { get; init; }

    public static MaterialResponse From(Material e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        SectionId = e.SectionId,
        Section = e.Section is null ? null : DocumentSectionResponse.From(e.Section),
        MaterialType = e.MaterialType,
        CustomType = e.CustomType,
        MaterialName = e.MaterialName,
        ManufacturerName = e.ManufacturerName,
        BatchNumber = e.BatchNumber,
        ReceivingDate = e.ReceivingDate,
        ExpiryDate = e.ExpiryDate,
        Code = e.Code,
        Location = e.Location,
        MediaProductId = e.MediaProductId,
        MediaProduct = e.MediaProduct is null ? null : MediaProductResponse.From(e.MediaProduct),
        AtccNumber = e.AtccNumber,
        OrganismId = e.OrganismId,
        Organism = e.Organism is null ? null : OrganismResponse.From(e.Organism),
        QuantityReceived = e.QuantityReceived,
        QuantityRemaining = e.QuantityRemaining,
        Unit = e.Unit,
        MinimumStockLevel = e.MinimumStockLevel,
        Purity = e.Purity,
        CreatedByUserId = e.CreatedByUserId,
        CreatedAt = e.CreatedAt,
        LastModifiedByUserId = e.LastModifiedByUserId,
        LastModifiedAt = e.LastModifiedAt,
        Status = e.Status,
        IsUsable = e.IsUsable,
    };
}

public class EquipmentInventoryResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public string InstrumentType { get; init; } = null!;
    public string ManufacturerName { get; init; } = null!;
    public string? SerialNumber { get; init; }
    public string? FirmwareVersion { get; init; }
    public string Code { get; init; } = null!;
    public string Location { get; init; } = null!;
    public DateTime? CalibrationDueDate { get; init; }
    public EquipmentOperationalStatus Status { get; init; }
    public int? SectionId { get; init; }
    public DocumentSectionResponse? Section { get; init; }
    public int CreatedByUserId { get; init; }
    public DateTime CreatedAt { get; init; }
    public int LastModifiedByUserId { get; init; }
    public DateTime LastModifiedAt { get; init; }
    public bool IsCalibrationOverdue { get; init; }

    public static EquipmentInventoryResponse From(EquipmentInventory e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        InstrumentType = e.InstrumentType,
        ManufacturerName = e.ManufacturerName,
        SerialNumber = e.SerialNumber,
        FirmwareVersion = e.FirmwareVersion,
        Code = e.Code,
        Location = e.Location,
        CalibrationDueDate = e.CalibrationDueDate,
        Status = e.Status,
        SectionId = e.SectionId,
        Section = e.Section is null ? null : DocumentSectionResponse.From(e.Section),
        CreatedByUserId = e.CreatedByUserId,
        CreatedAt = e.CreatedAt,
        LastModifiedByUserId = e.LastModifiedByUserId,
        LastModifiedAt = e.LastModifiedAt,
        IsCalibrationOverdue = e.IsCalibrationOverdue,
    };
}
