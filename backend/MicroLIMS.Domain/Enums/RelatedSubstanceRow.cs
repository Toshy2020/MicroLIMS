namespace MicroLIMS.Domain.Enums;

// Kind of a related-substances specification row (Plan 3): a named impurity, any unspecified
// impurity ("each"), the total of all impurities, or the total of the unnamed ones.
public enum RelatedSubstanceRow { Named = 0, UnspecifiedEach = 1, Total = 2, TotalOthers = 3 }
