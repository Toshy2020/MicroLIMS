using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// A saved set of table settings (filters, sort, columns, density, page size) for one registered table.
// It carries settings only, never data: opening it shows the viewer only the rows they may already read.
// StateJson is a JSON object with an integer property "v"; its size and shape are enforced by TableViewService.
public class TableView
{
    public int Id { get; set; }
    public int OwnerUserId { get; set; }
    public User? Owner { get; set; }
    public string TableKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TableViewScope Scope { get; set; }
    // Set exactly when Scope is Section (check constraint CK_TableViews_ScopeSection).
    public int? SectionId { get; set; }
    public DocumentSection? Section { get; set; }
    public string StateJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int UpdatedByUserId { get; set; }
}
