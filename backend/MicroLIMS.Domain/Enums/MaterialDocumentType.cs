namespace MicroLIMS.Domain.Enums;

// Document types accepted for a received material lot.
// COA is the primary type and is mandatory for DehydratedMedia,
// LyophilizedMicroorganism, and Supplement before consumption.
public enum MaterialDocumentType
{
    COA,
    SupplierCertificate,
    Specification,
    SDS,
    Other
}
