namespace MicroLIMS.Infrastructure.Storage;

public interface IFileStorageService
{
    Task<string> SaveAsync(string fileName, byte[] content);
    Task<byte[]> ReadAsync(string path);
}
