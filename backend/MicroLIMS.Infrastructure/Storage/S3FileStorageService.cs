using System.Net;
using Amazon.S3;
using Amazon.S3.Model;

namespace MicroLIMS.Infrastructure.Storage;

// Production implementation, written against the S3 API so any
// S3-compatible provider works; MicroLIMS runs it on Backblaze B2. Files
// survive container restarts and redeploys, which the Render container's
// local disk does not.
//
// legacyBasePath lets rows written by LocalFileStorageService before the
// move ("storage/documents/...") resolve to the same object key that a
// re-upload of the original file would use.
public class S3FileStorageService : IFileStorageService
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucketName;
    private readonly string? _legacyBasePath;

    public S3FileStorageService(IAmazonS3 s3, string bucketName, string? legacyBasePath)
    {
        if (string.IsNullOrWhiteSpace(bucketName))
            throw new ArgumentException("A bucket name is required.", nameof(bucketName));

        _s3 = s3;
        _bucketName = bucketName;
        _legacyBasePath = legacyBasePath;
    }

    public async Task<string> SaveAsync(string fileName, byte[] content)
    {
        var key = StorageKey.Normalize(fileName, legacyBasePath: null);

        using var body = new MemoryStream(content, writable: false);
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = body,
            // Send the body whole rather than as an aws-chunked stream:
            // S3-compatible providers support chunked signing unevenly, and
            // the content is already fully buffered.
            UseChunkEncoding = false
        });

        return key;
    }

    public async Task<byte[]> ReadAsync(string path)
    {
        var key = StorageKey.Normalize(path, _legacyBasePath);

        try
        {
            using var response = await _s3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            });

            using var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer);
            return buffer.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new StoredFileNotFoundException(key, ex);
        }
    }
}
