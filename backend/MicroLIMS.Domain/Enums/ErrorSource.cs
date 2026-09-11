namespace MicroLIMS.Domain.Enums;

// Which tier produced the ErrorLog entry.
public enum ErrorSource
{
    Backend = 0,
    Frontend = 1,
    Database = 2
}
