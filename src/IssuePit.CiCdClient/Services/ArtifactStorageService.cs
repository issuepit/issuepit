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

    // Local fallback storage path used when S3 is not configured.
    // Both the cicd-client and the API must resolve the same default so downloads work.
    private readonly string _localStorePath =
        configuration["CiCd__LocalArtifactStorePath"]
        ?? Path.Combine(Path.GetTempPath(), "issuepit-artifact-store");

    private volatile bool _bucketEnsured;

    /// <summary>
    /// Returns true when the storage service is configured (i.e. an S3 service URL is set).
    /// When false, uploads are skipped and <c>UploadArtifactAsync</c> returns <c>null</c>.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_serviceUrl);

    /// <summary>
    /// Counts the files contained in the artifact directory.
    /// act stores each artifact file as a <c>.zip</c> entry; this method counts the entries inside
    /// those inner zip files. Falls back to counting raw files for plain (non-zip) artifacts.
    /// </summary>
    /// <returns>A tuple of (fileCount, totalSizeBytes) reflecting the decompressed content.</returns>
    public static (int FileCount, long SizeBytes) CountArtifactFiles(string artifactDir)
    {
        var count = 0;
        long size = 0;

        foreach (var filePath in Directory.EnumerateFiles(artifactDir, "*", SearchOption.AllDirectories))
        {
            if (string.Equals(Path.GetExtension(filePath), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var zip = System.IO.Compression.ZipFile.OpenRead(filePath);
                    count += zip.Entries.Count;
                    size += zip.Entries.Sum(e => e.Length);
                    continue;
                }
                catch
                {
                    // Not a valid zip — fall through to count as a raw file.
                }
            }

            count++;
            try { size += new FileInfo(filePath).Length; } catch { /* ignore */ }
        }

        return (count, size);
    }

    /// <summary>
    /// Zips the given <paramref name="artifactDir"/> and uploads it to S3.
    /// Returns a tuple of (publicDownloadUrl, storageKey), or <c>(null, null)</c> if the service is not configured.
    /// </summary>
    /// <param name="artifactDir">Full path to the artifact directory (produced by the act artifact server).</param>
    /// <param name="artifactName">The artifact name (used in the S3 key).</param>
    /// <param name="runId">The CI/CD run ID (used in the S3 key for namespacing).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<(string? Url, string? Key)> UploadArtifactAsync(
        string artifactDir,
        string artifactName,
        Guid runId,
        CancellationToken ct = default)
    {
        if (!IsConfigured) return (null, null);

        var safeRunId = runId.ToString("N");
        var safeName = SanitizeArtifactName(artifactName);
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
        return (url, key);
    }

    /// <summary>
    /// Saves the artifact as a ZIP file to the local artifact store path.
    /// Used as a fallback when S3 storage is not configured so that artifacts remain downloadable
    /// on single-machine (e.g. Aspire) deployments where the API and cicd-client share the filesystem.
    /// Returns a tuple of (null, storageKey) where storageKey has the prefix <c>local:</c>.
    /// </summary>
    public async Task<(string? Url, string? Key)> SaveLocallyAsync(
        string artifactDir,
        string artifactName,
        Guid runId,
        CancellationToken ct = default)
    {
        var safeRunId = runId.ToString("N");
        var safeName = SanitizeArtifactName(artifactName);

        var runDir = Path.Combine(_localStorePath, safeRunId);
        Directory.CreateDirectory(runDir);

        var zipPath = Path.Combine(runDir, $"{safeName}.zip");
        using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None,
                   bufferSize: 65536, useAsync: true))
        {
            await ZipDirectoryAsync(artifactDir, fileStream, ct);
        }

        var key = $"local:{safeRunId}/{safeName}.zip";
        logger.LogInformation("Saved artifact '{Name}' for run {RunId} locally: {Path}", artifactName, runId, zipPath);
        return (null, key);
    }

    /// <summary>
    /// Returns a sanitized artifact name safe for use in file paths and S3 keys.
    /// Strips characters that are not letters, digits, hyphens, underscores or dots;
    /// collapses consecutive dots to prevent path traversal; falls back to "artifact".
    /// </summary>
    private static string SanitizeArtifactName(string name)
    {
        var safe = new string(name.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.').ToArray());
        // Prevent path-traversal: replace runs of two or more dots.
        safe = System.Text.RegularExpressions.Regex.Replace(safe, @"\.{2,}", "_");
        return string.IsNullOrEmpty(safe) || safe.TrimStart('.').Length == 0 ? "artifact" : safe;
    }

    /// <summary>
    /// Zips the contents of <paramref name="sourceDir"/> into <paramref name="destination"/>.
    /// Files that are themselves ZIP archives (as produced by the act artifact server) are extracted
    /// and their entries are written directly into the output archive so the download is a clean,
    /// single-level ZIP with the actual file names.
    /// </summary>
    private static async Task ZipDirectoryAsync(string sourceDir, Stream destination, CancellationToken ct)
    {
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);

        foreach (var filePath in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();

            // act stores artifact files as individual zip archives. Re-package their contents
            // directly so the resulting download archive is a clean, single-level ZIP.
            if (string.Equals(Path.GetExtension(filePath), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                var handledAsZip = false;
                try
                {
                    using var innerZip = ZipFile.OpenRead(filePath);
                    foreach (var innerEntry in innerZip.Entries)
                    {
                        ct.ThrowIfCancellationRequested();
                        // Skip directory entries.
                        if (innerEntry.FullName.EndsWith('/') || innerEntry.FullName.EndsWith('\\'))
                            continue;
                        var outEntry = archive.CreateEntry(innerEntry.FullName, CompressionLevel.Optimal);
                        using var outStream = outEntry.Open();
                        using var innerStream = innerEntry.Open();
                        await innerStream.CopyToAsync(outStream, ct);
                    }
                    handledAsZip = true;
                }
                catch
                {
                    // Not a valid zip — fall through to add as a raw file.
                }

                if (handledAsZip) continue;
            }

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
