using System.IO.Compression;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace IssuePit.CiCdClient.Services;

/// <summary>
/// Uploads CI/CD artifacts to S3-compatible storage and returns public download URLs.
/// Reuses the <c>ImageStorage__*</c> configuration keys that the API project uses.
///
/// Reads from:
/// <list type="bullet">
///   <item><c>ImageStorage__ServiceUrl</c> — S3 service URL (empty = AWS default; LocalStack: http://localhost:4566)</item>
///   <item><c>ImageStorage__AccessKey</c> — AWS access key ID (default: <c>test</c>)</item>
///   <item><c>ImageStorage__SecretKey</c> — AWS secret access key (default: <c>test</c>)</item>
///   <item><c>ImageStorage__BucketName</c> — S3 bucket name (default: <c>issuepit-uploads</c>)</item>
///   <item><c>ImageStorage__PublicBaseUrl</c> — public base URL for generated links (optional)</item>
///   <item><c>ImageStorage__Region</c> — AWS region (default: <c>us-east-1</c>)</item>
/// </list>
/// When <c>ImageStorage__ServiceUrl</c> is not configured, S3 upload is skipped and <c>null</c> is returned.
/// </summary>
public class ArtifactStorageService(IConfiguration configuration, ILogger<ArtifactStorageService> logger)
{
    private readonly string? _serviceUrl = configuration["ImageStorage__ServiceUrl"];
    private readonly string _accessKey = configuration["ImageStorage__AccessKey"] ?? "test";
    private readonly string _secretKey = configuration["ImageStorage__SecretKey"] ?? "test";
    private readonly string _bucketName = configuration["ImageStorage__BucketName"] ?? "issuepit-uploads";
    private readonly string? _publicBaseUrl = configuration["ImageStorage__PublicBaseUrl"];
    private readonly string _region = configuration["ImageStorage__Region"] ?? "us-east-1";

    private volatile bool _bucketEnsured;

    /// <summary>
    /// Returns true when the storage service is configured (i.e. an S3 service URL is set).
    /// When false, uploads are skipped and <c>UploadArtifactAsync</c> returns <c>null</c>.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_serviceUrl);

    /// <summary>
    /// Zips the given <paramref name="artifactDir"/> and uploads it to S3.
    /// Returns the public download URL, or <c>null</c> if the service is not configured.
    /// </summary>
    /// <param name="artifactDir">Full path to the artifact directory (produced by the act artifact server).</param>
    /// <param name="artifactName">The artifact name (used in the S3 key).</param>
    /// <param name="runId">The CI/CD run ID (used in the S3 key for namespacing).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<string?> UploadArtifactAsync(
        string artifactDir,
        string artifactName,
        Guid runId,
        CancellationToken ct = default)
    {
        if (!IsConfigured) return null;

        var safeRunId = runId.ToString("N");
        var safeName = new string(artifactName.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.').ToArray());
        // Prevent path-traversal: replace consecutive dots and reject empty result.
        safeName = System.Text.RegularExpressions.Regex.Replace(safeName, @"\.{2,}", "_");
        if (string.IsNullOrEmpty(safeName) || safeName.TrimStart('.').Length == 0) safeName = "artifact";
        var key = $"artifacts/{safeRunId}/{safeName}.zip";

        using var memStream = new MemoryStream();
        await ZipDirectoryAsync(artifactDir, memStream, ct);
        memStream.Position = 0;

        using var s3 = CreateClient();

        if (!_bucketEnsured)
        {
            await EnsureBucketExistsAsync(s3, ct);
            _bucketEnsured = true;
        }

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = memStream,
            ContentType = "application/zip",
        };

        await RetryS3Async(() =>
        {
            if (memStream.CanSeek) memStream.Position = 0;
            return s3.PutObjectAsync(request, ct);
        }, ct);

        var url = BuildPublicUrl(key);
        logger.LogInformation("Uploaded artifact '{Name}' for run {RunId} to S3: {Url}", artifactName, runId, url);
        return url;
    }

    private static async Task ZipDirectoryAsync(string sourceDir, Stream destination, CancellationToken ct)
    {
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);

        foreach (var filePath in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            var entryName = Path.GetRelativePath(sourceDir, filePath).Replace('\\', '/');
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            using var fileStream = File.OpenRead(filePath);
            await fileStream.CopyToAsync(entryStream, ct);
        }
    }

    private IAmazonS3 CreateClient()
    {
        var credentials = new BasicAWSCredentials(_accessKey, _secretKey);
        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(_region),
            ForcePathStyle = true, // Required for LocalStack and B2
        };

        if (!string.IsNullOrWhiteSpace(_serviceUrl))
            config.ServiceURL = _serviceUrl;

        return new AmazonS3Client(credentials, config);
    }

    private async Task EnsureBucketExistsAsync(IAmazonS3 s3, CancellationToken ct)
    {
        const int maxAttempts = 5;
        Exception? lastEx = null;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                await s3.PutBucketAsync(new PutBucketRequest
                {
                    BucketName = _bucketName,
                    UseClientRegion = true,
                    ObjectOwnership = ObjectOwnership.ObjectWriter,
                }, ct);
                return;
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
            {
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts - 1)
            {
                lastEx = ex;
                var delay = TimeSpan.FromSeconds(attempt + 1);
                logger.LogWarning(ex, "S3 bucket creation attempt {Attempt}/{Max} failed, retrying in {Delay}s",
                    attempt + 1, maxAttempts, delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }
            catch (Exception ex)
            {
                lastEx = ex;
            }
        }
        throw new InvalidOperationException($"Failed to ensure S3 bucket '{_bucketName}' after {maxAttempts} attempts.", lastEx);
    }

    private async Task RetryS3Async(Func<Task> operation, CancellationToken ct)
    {
        const int maxAttempts = 3;
        Exception? lastEx = null;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                await operation();
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts - 1)
            {
                lastEx = ex;
                var delay = TimeSpan.FromSeconds(attempt + 1);
                logger.LogWarning(ex, "S3 operation attempt {Attempt}/{Max} failed, retrying in {Delay}s",
                    attempt + 1, maxAttempts, delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }
            catch (Exception ex)
            {
                lastEx = ex;
            }
        }
        throw new InvalidOperationException($"S3 operation failed after {maxAttempts} attempts.", lastEx);
    }

    private string BuildPublicUrl(string key)
    {
        if (!string.IsNullOrWhiteSpace(_publicBaseUrl))
            return $"{_publicBaseUrl.TrimEnd('/')}/{key}";

        if (!string.IsNullOrWhiteSpace(_serviceUrl))
            return $"{_serviceUrl.TrimEnd('/')}/{_bucketName}/{key}";

        return $"https://s3.{_region}.amazonaws.com/{_bucketName}/{key}";
    }
}
