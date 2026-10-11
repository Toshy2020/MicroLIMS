using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.DTOs;

public record AddMediaStorageEntryRequest(
    string Form,
    int Quantity,
    int? EquipmentId = null,
    string? LocationText = null);

public record MoveMediaStorageEntryRequest(
    int Quantity,
    int? EquipmentId = null,
    string? LocationText = null,
    string Reason = "");

public class MediaStorageEntryResponse
{
    public int Id { get; init; }
    public int MediaId { get; init; }
    public string Form { get; init; } = null!;
    public int Quantity { get; init; }
    public int? EquipmentId { get; init; }
    public string? LocationText { get; init; }
    public string? EquipmentCode { get; init; }
    public string? EquipmentName { get; init; }
    public string? EquipmentLocation { get; init; }
    public DateTime CreatedAt { get; init; }
    public int CreatedByUserId { get; init; }
    public DateTime UpdatedAt { get; init; }

    public static MediaStorageEntryResponse From(MediaStorageEntry e)
    {
        string? eqName = null;
        if (e.Equipment != null)
        {
            eqName = string.IsNullOrWhiteSpace(e.Equipment.ManufacturerName)
                ? e.Equipment.InstrumentType
                : $"{e.Equipment.InstrumentType} - {e.Equipment.ManufacturerName}".Trim(' ', '-');
        }

        return new MediaStorageEntryResponse
        {
            Id = e.Id,
            MediaId = e.MediaId,
            Form = e.Form.ToString(),
            Quantity = e.Quantity,
            EquipmentId = e.EquipmentId,
            LocationText = e.LocationText,
            EquipmentCode = e.Equipment?.Code,
            EquipmentName = eqName,
            EquipmentLocation = e.Equipment?.Location,
            CreatedAt = e.CreatedAt,
            CreatedByUserId = e.CreatedByUserId,
            UpdatedAt = e.UpdatedAt
        };
    }
}
