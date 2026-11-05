namespace MicroLIMS.Domain.Enums;

// What an ICP test reports: Assay = % of the target concentration; Impurity = the element
// concentration in ppm, judged against the item's not-more-than limit (0 = not detected).
public enum IcpTestMode { Assay = 0, Impurity = 1 }
