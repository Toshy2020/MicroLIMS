namespace MicroLIMS.Application.Abstractions.Storage;

// Where the files physically live, without secrets, plus the one operation the
// application never needs otherwise: removing an object (used to clean up the
// admin probe). Implemented by the storage providers beside IFileStorageService.
public record StorageDescription(string Provider, string Location, long? FreeBytes, long? TotalBytes);

public interface IStorageDiagnostics
{
    StorageDescription Describe();
    Task DeleteAsync(string key);
}
