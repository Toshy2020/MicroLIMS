namespace MicroLIMS.Domain.Enums;

// Who a saved table view is for. Personal: its owner only. Section: every member of SectionId.
public enum TableViewScope
{
    Personal = 0,
    Section = 1
}
