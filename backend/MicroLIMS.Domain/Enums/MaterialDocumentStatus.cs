namespace MicroLIMS.Domain.Enums;

// Lifecycle status of a material lot document.
// Only Current documents satisfy the mandatory COA requirement.
// Superseded and Voided documents remain historically accessible.
public enum MaterialDocumentStatus
{
    Current,
    Superseded,
    Voided
}
