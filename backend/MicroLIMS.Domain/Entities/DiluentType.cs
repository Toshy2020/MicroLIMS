namespace MicroLIMS.Domain.Entities;

// Section Head master list used in Test Preparation. When
// RequiresBatchTracking is true (e.g. TSB), selection must be backed by
// a real Media lot filtered to GPT-released + Active + non-expired - of
// this Culture Media product (MediaProductId), any lot of it, not just its old class. When false
// (e.g. Buffer), no lot binding is needed at all.
public class DiluentType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool RequiresBatchTracking { get; set; }
    public int? MediaProductId { get; set; } // set only when RequiresBatchTracking
    public MediaProduct? MediaProduct { get; set; }
}
