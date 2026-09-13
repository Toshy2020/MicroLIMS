using System.Net;
using System.Reflection;
using Amazon.S3;
using Amazon.S3.Model;
using MicroLIMS.Infrastructure.Storage;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class StorageKeyTests
{
    [Theory]
    [InlineData("documents/2/2_controlledpdf.pdf", "storage", "documents/2/2_controlledpdf.pdf")]
    [InlineData("storage/documents/2/2_controlledpdf.pdf", "storage", "documents/2/2_controlledpdf.pdf")]
    [InlineData(@"storage\documents/2/2_controlledpdf.pdf", "storage", "documents/2/2_controlledpdf.pdf")]
    [InlineData("./storage/documents/2/2_controlledpdf.pdf", "storage", "documents/2/2_controlledpdf.pdf")]
    [InlineData("/app/storage/documents/2/2_controlledpdf.pdf", "/app/storage", "documents/2/2_controlledpdf.pdf")]
    [InlineData("storage-archive/report.pdf", "storage", "storage-archive/report.pdf")]
    [InlineData("storage/documents/2/2_controlledpdf.pdf", null, "storage/documents/2/2_controlledpdf.pdf")]
    public void Normalize_ResolvesLegacyPathsAndRelativeKeysToTheSameKey(string stored, string? basePath, string expected)
    {
        Assert.Equal(expected, StorageKey.Normalize(stored, basePath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("storage/")]
    [InlineData("documents/../../etc/passwd")]
    public void Normalize_RejectsEmptyAndTraversingKeys(string stored)
    {
        Assert.Throws<ArgumentException>(() => StorageKey.Normalize(stored, "storage"));
    }
}

public class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "microlims-storage-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_baseDir))
            Directory.Delete(_baseDir, recursive: true);
    }

    [Fact]
    public async Task SaveAsync_ReturnsTheRelativeKey_AndReadAsyncResolvesIt()
    {
        var storage = new LocalFileStorageService(_baseDir);

        var key = await storage.SaveAsync("documents/7/12_controlledpdf.pdf", [1, 2, 3]);

        Assert.Equal("documents/7/12_controlledpdf.pdf", key);
        Assert.True(File.Exists(Path.Combine(_baseDir, "documents", "7", "12_controlledpdf.pdf")));
        Assert.Equal(new byte[] { 1, 2, 3 }, await storage.ReadAsync(key));
    }

    [Fact]
    public async Task ReadAsync_AcceptsTheCombinedPathOlderRowsPersisted()
    {
        var storage = new LocalFileStorageService(_baseDir);
        await storage.SaveAsync("documents/7/12_controlledpdf.pdf", [4, 5, 6]);

        var legacyValue = Path.Combine(_baseDir, "documents/7/12_controlledpdf.pdf");

        Assert.Equal(new byte[] { 4, 5, 6 }, await storage.ReadAsync(legacyValue));
    }

    [Fact]
    public async Task ReadAsync_MissingFile_ThrowsStoredFileNotFound()
    {
        var storage = new LocalFileStorageService(_baseDir);
        await storage.SaveAsync("documents/7/12_controlledpdf.pdf", [1]);

        var ex = await Assert.ThrowsAsync<StoredFileNotFoundException>(() =>
            storage.ReadAsync("documents/7/missing.pdf"));

        Assert.Equal("documents/7/missing.pdf", ex.Key);
    }

    [Fact]
    public async Task ReadAsync_MissingDirectory_ThrowsStoredFileNotFound()
    {
        // The production failure: after a container restart the whole
        // documents/{revisionId} directory is gone, not just the file.
        var storage = new LocalFileStorageService(_baseDir);

        await Assert.ThrowsAsync<StoredFileNotFoundException>(() =>
            storage.ReadAsync("documents/2/2_controlledpdf.pdf"));
    }
}

public class S3FileStorageServiceTests
{
    private const string Bucket = "microlims-files";

    [Fact]
    public async Task SaveAsync_PutsTheWholeBodyUnderTheRelativeKey()
    {
        var s3 = FakeS3.Create(out var fake);
        var storage = new S3FileStorageService(s3, Bucket, legacyBasePath: "storage");

        var key = await storage.SaveAsync("documents/2/2_controlledpdf.pdf", [37, 80, 68, 70]);

        Assert.Equal("documents/2/2_controlledpdf.pdf", key);
        var put = Assert.Single(fake.Puts);
        Assert.Equal(Bucket, put.Bucket);
        Assert.Equal(key, put.Key);
        Assert.Equal(new byte[] { 37, 80, 68, 70 }, put.Body);
        Assert.False(put.UseChunkEncoding);
    }

    [Fact]
    public async Task ReadAsync_ResolvesALegacyLocalPathToTheSameObject()
    {
        var s3 = FakeS3.Create(out var fake);
        fake.Objects["documents/2/2_controlledpdf.pdf"] = [1, 2, 3];
        var storage = new S3FileStorageService(s3, Bucket, legacyBasePath: "storage");

        var content = await storage.ReadAsync("storage/documents/2/2_controlledpdf.pdf");

        Assert.Equal(new byte[] { 1, 2, 3 }, content);
    }

    [Fact]
    public async Task ReadAsync_MissingObject_ThrowsStoredFileNotFound()
    {
        var s3 = FakeS3.Create(out _);
        var storage = new S3FileStorageService(s3, Bucket, legacyBasePath: "storage");

        var ex = await Assert.ThrowsAsync<StoredFileNotFoundException>(() =>
            storage.ReadAsync("documents/9/missing.pdf"));

        Assert.Equal("documents/9/missing.pdf", ex.Key);
    }

    [Fact]
    public async Task ReadAsync_OtherS3Errors_AreNotReportedAsMissing()
    {
        var s3 = FakeS3.Create(out var fake);
        fake.FailWith = new AmazonS3Exception("Access Denied") { StatusCode = HttpStatusCode.Forbidden };
        var storage = new S3FileStorageService(s3, Bucket, legacyBasePath: "storage");

        await Assert.ThrowsAsync<AmazonS3Exception>(() => storage.ReadAsync("documents/2/2_controlledpdf.pdf"));
    }
}

// IAmazonS3 has hundreds of members. DispatchProxy lets this fake implement
// only the two calls S3FileStorageService makes and fail loudly on any other.
public class FakeS3 : DispatchProxy
{
    public Dictionary<string, byte[]> Objects { get; } = new();
    public List<(string Bucket, string Key, byte[] Body, bool? UseChunkEncoding)> Puts { get; } = new();
    public Exception? FailWith { get; set; }

    public static IAmazonS3 Create(out FakeS3 fake)
    {
        var proxy = DispatchProxy.Create<IAmazonS3, FakeS3>();
        fake = (FakeS3)(object)proxy;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        switch (targetMethod?.Name, args?.FirstOrDefault())
        {
            case ("PutObjectAsync", PutObjectRequest put):
                using (var body = new MemoryStream())
                {
                    put.InputStream.CopyTo(body);
                    Puts.Add((put.BucketName, put.Key, body.ToArray(), put.UseChunkEncoding));
                }
                return Task.FromResult(new PutObjectResponse());

            case ("GetObjectAsync", GetObjectRequest get):
                if (FailWith != null)
                    return Task.FromException<GetObjectResponse>(FailWith);
                if (!Objects.TryGetValue(get.Key, out var content))
                    return Task.FromException<GetObjectResponse>(
                        new AmazonS3Exception("The specified key does not exist.") { StatusCode = HttpStatusCode.NotFound });
                return Task.FromResult(new GetObjectResponse { ResponseStream = new MemoryStream(content) });

            default:
                throw new NotSupportedException($"FakeS3 does not implement {targetMethod?.Name}.");
        }
    }
}
