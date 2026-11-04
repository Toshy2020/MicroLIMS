namespace MicroLIMS.Domain.Enums;

public enum SampleDocumentType
{
    SupplierCoa = 0,
    Other = 1,
    ExternalLabReport = 2
}

// Documents are never deleted; voiding keeps the file and marks the row.
public enum SampleDocumentStatus
{
    Current = 0,
    Voided = 1
}
