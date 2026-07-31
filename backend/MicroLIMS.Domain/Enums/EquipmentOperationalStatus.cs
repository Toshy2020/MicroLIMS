namespace MicroLIMS.Domain.Enums;

// Operational status of a registered instrument (EquipmentInventory).
// The print view excludes OutOfService and Retired instruments.
public enum EquipmentOperationalStatus
{
    InService,
    OutOfService,
    Retired
}
