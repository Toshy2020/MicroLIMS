namespace MicroLIMS.Domain.Enums;

// Media Inventory lifecycle (gap analysis - Missing Laboratory Modules).
public enum MediaStatus
{
    Prepared,
    Active,
    Expired,
    QuarantineFailed,
    Destroyed,
    OutOfStock
}
