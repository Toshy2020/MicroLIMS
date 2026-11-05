namespace MicroLIMS.Domain.Enums;

// What a culture-medium additive (e.g. a neutralizer) was taken from.
public enum MediaAdditiveKind
{
    StockLot,           // a Materials Stock lot; deducted from stock
    PreparedSolution    // a prepared solution (SolutionPreparation); recorded only, never deducted
}
