namespace MicroLIMS.Domain.Entities;

// Test Preparation step - Product/RM/PM/Water only, once per Sample
// (shared across all its TestOrders, since dilution/neutralization
// happens once even though results split across TAMC/TYMC/pathogens).
public class SamplePreparation
{
    public int Id { get; set; }
    public int SampleId { get; set; }
    public Sample? Sample { get; set; }

    public decimal Amount { get; set; }
    public string Unit { get; set; } = string.Empty; // ml/gm/bottle/cap/25cm2

    public string Technique { get; set; } = string.Empty; // "PourPlate" or "Filtration"
    public decimal? FiltrationVolume { get; set; }
    public decimal? WashingVolume { get; set; }

    public int DiluentTypeId { get; set; }
    public DiluentType? DiluentType { get; set; }
    public int? DiluentMediaId { get; set; } // set only when DiluentType.RequiresBatchTracking
    public Media? DiluentMedia { get; set; }

    public int NeutralizerId { get; set; }
    public Neutralizer? Neutralizer { get; set; }

    public int PreparedByUserId { get; set; }
    public DateTime PreparedAt { get; set; } = DateTime.UtcNow;
}
