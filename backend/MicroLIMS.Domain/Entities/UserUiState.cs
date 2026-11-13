namespace MicroLIMS.Domain.Entities;

// One interface preference of one user (tour seen, last release note, favourites).
// Not laboratory data. Key and value limits are enforced by UserUiStateService.
public class UserUiState
{
    public int UserId { get; set; }
    public User? User { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
