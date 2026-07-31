namespace MicroLIMS.Domain.Enums;

// The predefined unit list (Mohamed confirmed dropdown, not free text).
// MaterialService.DefaultUnitFor maps each MaterialType to a
// sensible default; the analyst can still pick any unit from this list.
public enum MaterialUnit
{
    Gram,
    Kilogram,
    Milliliter,
    Liter,
    Disc,
    Vial,
    Kit,
    Piece,
    Bottle,
    Pack
}
