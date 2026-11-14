namespace MicroLIMS.Domain.Entities;

// The view a user opens a table with when the address carries no table state. One per user and table.
public class UserTableDefault
{
    public int UserId { get; set; }
    public User? User { get; set; }
    public string TableKey { get; set; } = string.Empty;
    public int TableViewId { get; set; }
    public TableView? TableView { get; set; }
}
