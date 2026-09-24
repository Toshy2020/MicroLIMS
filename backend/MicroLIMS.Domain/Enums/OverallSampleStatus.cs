namespace MicroLIMS.Domain.Enums;

// What a sample's outcome reads as across every laboratory - computed,
// never stored. A rejection by any lab wins at once, while Sample.Status
// keeps following the labs still at work.
public enum OverallSampleStatus { InProgress, Approved, Rejected, RetestRequested, Voided, Cancelled }
