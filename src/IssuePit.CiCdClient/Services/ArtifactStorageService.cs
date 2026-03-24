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
/// Reads from (env var name → configuration key):
/// <list type="bullet">
///   <item><c>ImageStorage__ServiceUrl</c> → <c>ImageStorage:ServiceUrl</c> — S3 service URL (empty = AWS default; LocalStack: http://localhost:4566)</item>
///   <item><c>ImageStorage__AccessKey</c> → <c>ImageStorage:AccessKey</c> — AWS access key ID (default: <c>test</c>)</item>
///   <item><c>ImageStorage__SecretKey</c> → <c>ImageStorage:SecretKey</c> — AWS secret access key (default: <c>test</c>)</item>
///   <item><c>ImageStorage__BucketName</c> → <c>ImageStorage:BucketName</c> — S3 bucket name (default: <c>issuepit-uploads</c>)</item>
///   <item><c>ImageStorage__PublicBaseUrl</c> → <c>ImageStorage:PublicBaseUrl</c> — public base URL for generated links (optional)</item>
///   <item><c>ImageStorage__Region</c> → <c>ImageStorage:Region</c> — AWS region (default: <c>us-east-1</c>)</item>
/// </list>
/// When <c>ImageStorage:ServiceUrl</c> is not configured, S3 upload is skipped and <c>null</c> is returned.
/// </summary>
public class ArtifactStorageService(IConfiguration configuration, ILogger<ArtifactStorageService> logger)
{
    private readonly string? _serviceUrl = configuration["ImageStorage:ServiceUrl"];
    private readonly string _accessKey = configuration["ImageStorage:AccessKey"] ?? "test";
    private readonly string _secretKey = configuration["ImageStorage:SecretKey"] ?? "test";
    private readonly string _bucketName = configuration["ImageStorage:BucketName"] ?? "issuepit-uploads";
    private readonly string? _publicBaseUrl = configuration["ImageStorage:PublicBaseUrl"];
    private readonly string _region = configuration["ImageStorage:Region"] ?? "us-east-1";

    private volatile bool _bucketEnsured;

    /// <summary>
    /// Returns true when the storage service is configured (i.e. an S3 service URL is set via <c>ImageStorage:ServiceUrl</c>).
    /// When false, uploads are skipped and <c>UploadArtifactAsync</c> returns <c>null</c>.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_serviceUrl);

    /// <summary>
    /// Counts the files contained in the artifact directory.
    /// act stores each artifact file as a <c>.zip</c> entry; this method counts the entries inside
    /// those inner zip files. Falls back to counting raw files for plain (non-zip) artifacts.
    /// Also handles extensionless files stored by act v7+ (direct-upload format): tries to open
    /// them as zip archives before falling back to treating them as a single raw file.
    /// </summary>
    /// <returns>A tuple of (fileCount, totalSizeBytes) reflecting the decompressed content.</returns>
    public static (int FileCount, long SizeBytes) CountArtifactFiles(string artifactDir)
    {
        var count = 0;
        long size = 0;

        foreach (var filePath in Directory.EnumerateFiles(artifactDir, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(filePath);
            // Try to open as a zip archive for files with .zip extension or no extension at all
            // (act v7+ stores direct-upload artifacts without a .zip extension).
            if (string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(ext))
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
    /// Returns a tuple of (publicDownloadUrl, storageKey, unwrappedContentType).
    /// When <paramref name="unwrapIfSingleFile"/> is <c>true</c> and the artifact contains exactly
    /// one file of a supported type (pdf, png), the raw file is uploaded directly instead of a ZIP
    /// and <c>unwrappedContentType</c> contains its MIME type; otherwise it is <c>null</c>.
    /// Returns <c>(null, null, null)</c> if the service is not configured.
    /// </summary>
    /// <param name="artifactDir">Full path to the artifact directory (produced by the act artifact server).</param>
    /// <param name="artifactName">The artifact name (used in the S3 key).</param>
    /// <param name="runId">The CI/CD run ID (used in the S3 key for namespacing).</param>
    /// <param name="unwrapIfSingleFile">When <c>true</c>, attempt to upload the raw file for single-file artifacts of supported types.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<(string? Url, string? Key, string? UnwrappedContentType)> UploadArtifactAsync(
        string artifactDir,
        string artifactName,
        Guid runId,
        bool unwrapIfSingleFile = false,
        CancellationToken ct = default)
    {
        if (!IsConfigured) return (null, null, null);

        var safeRunId = runId.ToString("N");
        var safeName = SanitizeArtifactName(artifactName);

        using var s3 = CreateClient();

        if (!_bucketEnsured)
        {
            await EnsureBucketExistsAsync(s3, ct);
            _bucketEnsured = true;
        }

        // Attempt to unwrap a single-file artifact of a supported type.
        if (unwrapIfSingleFile)
        {
            var unwrapped = TryExtractSingleSupportedFile(artifactDir);
            if (unwrapped.HasValue)
            {
                var (fileStream, mimeType, fileExt) = unwrapped.Value;
                await using (fileStream)
                {
                    var unwrappedKey = $"artifacts/{safeRunId}/{safeName}{fileExt}";
                    var unwrappedRequest = new PutObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = unwrappedKey,
                        InputStream = fileStream,
                        ContentType = mimeType,
                    };
                    await RetryS3Async(() =>
                    {
                        if (fileStream.CanSeek) fileStream.Position = 0;
                        return s3.PutObjectAsync(unwrappedRequest, ct);
                    }, ct);

                    var unwrappedUrl = BuildPublicUrl(unwrappedKey);
                    logger.LogInformation("Uploaded unwrapped artifact '{Name}' for run {RunId} to S3: {Url}", artifactName, runId, unwrappedUrl);
                    return (unwrappedUrl, unwrappedKey, mimeType);
                }
            }
        }

        var key = $"artifacts/{safeRunId}/{safeName}.zip";

        using var memStream = new MemoryStream();
        await ZipDirectoryAsync(artifactDir, memStream, ct);
        memStream.Position = 0;

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
        return (url, key, null);
    }

    /// <summary>
    /// Tries to extract a single file of a supported type (pdf, png) from the artifact directory.
    /// Returns the file stream, MIME type, and file extension, or <c>null</c> when the artifact does
    /// not qualify (more than one file, or the file type is not supported).
    /// </summary>
    private static (Stream FileStream, string MimeType, string FileExt)? TryExtractSingleSupportedFile(string artifactDir)
    {
        // Collect all "logical" files — unwrapping inner zip archives just like ZipDirectoryAsync does.
        string? singleEntryName = null;
        var logicalCount = 0;

        foreach (var filePath in Directory.EnumerateFiles(artifactDir, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(filePath);
            if (string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(ext))
            {
                try
                {
                    using var innerZip = ZipFile.OpenRead(filePath);
                    var innerEntries = innerZip.Entries
                        .Where(e => !e.FullName.EndsWith('/') && !e.FullName.EndsWith('\\'))
                        .ToList();
                    if (innerEntries.Count == 0) continue;
                    logicalCount += innerEntries.Count;
                    if (logicalCount > 1) return null;
                    singleEntryName = innerEntries[0].FullName;
                    continue;
                }
                catch { /* fall through */ }
            }

            logicalCount++;
            if (logicalCount > 1) return null;
            singleEntryName = Path.GetFileName(filePath);
        }

        if (logicalCount != 1 || singleEntryName is null) return null;

        var singleExt = Path.GetExtension(singleEntryName).ToLowerInvariant();
        var mimeType = singleExt switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            _ => null,
        };
        if (mimeType is null) return null;

        // Re-scan to open the actual file stream.
        foreach (var filePath in Directory.EnumerateFiles(artifactDir, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(filePath);
            if (string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(ext))
            {
                try
                {
                    var innerZip = ZipFile.OpenRead(filePath);
                    var matchingEntry = innerZip.Entries
                        .FirstOrDefault(e => !e.FullName.EndsWith('/') && !e.FullName.EndsWith('\\'));
                    if (matchingEntry is null) { innerZip.Dispose(); continue; }

                    // Copy entry into a MemoryStream so the archive can be closed.
                    var ms = new MemoryStream((int)matchingEntry.Length);
                    using var entryStream = matchingEntry.Open();
                    entryStream.CopyTo(ms);
                    innerZip.Dispose();
                    ms.Position = 0;
                    return (ms, mimeType, singleExt);
                }
                catch { /* fall through */ }
            }
            else if (string.Equals(Path.GetExtension(filePath), singleExt, StringComparison.OrdinalIgnoreCase))
            {
                return (File.OpenRead(filePath), mimeType, singleExt);
            }
        }

        return null;
    }

    /// <summary>
    /// Uploads all files from <paramref name="localDir"/> to <c>artifacts-raw/{runId}/</c> in S3,
    /// preserving the relative path structure produced by the act artifact server.
    /// Returns the number of objects uploaded.
    /// No-op when S3 is not configured or the directory does not exist / is empty.
    /// </summary>
    public async Task<int> UploadRawArtifactsAsync(Guid runId, string localDir, CancellationToken ct = default)
    {
        if (!IsConfigured) return 0;
        if (!Directory.Exists(localDir)) return 0;

        var prefix = $"artifacts-raw/{runId:N}/";
        using var s3 = CreateClient();

        if (!_bucketEnsured)
        {
            await EnsureBucketExistsAsync(s3, ct);
            _bucketEnsured = true;
        }

        var uploaded = 0;
        var canonicalLocalDir = Path.GetFullPath(localDir);
        foreach (var filePath in Directory.EnumerateFiles(localDir, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            // Use forward slashes for S3 keys; GetRelativePath uses OS separator.
            var relativePath = Path.GetRelativePath(canonicalLocalDir, filePath).Replace('\\', '/');
            var key = $"{prefix}{relativePath}";

            await RetryS3Async(async () =>
            {
                // Open fresh stream on each attempt so retries work correctly.
                await using var fileStream = File.OpenRead(filePath);
                await s3.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key,
                    InputStream = fileStream,
                }, ct);
            }, ct);
            uploaded++;
        }

        return uploaded;
    }

    /// <summary>
    /// Downloads all raw artifact files uploaded by the container from
    /// <c>artifacts-raw/{runId}/</c> in S3 to <paramref name="localDir"/>,
    /// preserving the sub-directory structure produced by the act artifact server.
    /// Returns the number of objects downloaded.
    /// No-op when S3 is not configured.
    /// </summary>
    public async Task<int> DownloadRawArtifactsAsync(Guid runId, string localDir, CancellationToken ct = default)
    {
        if (!IsConfigured) return 0;

        var prefix = $"artifacts-raw/{runId:N}/";
        using var s3 = CreateClient();
        var downloaded = 0;

        string? continuationToken = null;
        do
        {
            var listResponse = await s3.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix,
                ContinuationToken = continuationToken,
            }, ct);

            foreach (var obj in listResponse.S3Objects)
            {
                ct.ThrowIfCancellationRequested();
                var relativeKey = obj.Key[prefix.Length..];
                if (string.IsNullOrEmpty(relativeKey))
                    continue;
                // S3 keys always use forward slashes; split and recombine via Path.Combine
                // so the local path uses the OS-appropriate separator (handles Windows).
                var segments = relativeKey.Split('/');
                var localPath = Path.Combine([localDir, .. segments]);
                Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
                using var getResponse = await s3.GetObjectAsync(obj.BucketName, obj.Key, ct);
                await using var fileStream = File.Create(localPath);
                await getResponse.ResponseStream.CopyToAsync(fileStream, ct);
                downloaded++;
            }

            continuationToken = listResponse.IsTruncated == true ? listResponse.NextContinuationToken : null;
        } while (continuationToken is not null);

        return downloaded;
    }

    /// <summary>
    /// Deletes all raw artifact objects uploaded by the container from
    /// <c>artifacts-raw/{runId}/</c> in S3 after they have been processed.
    /// Best-effort: does not throw on individual delete failures.
    /// No-op when S3 is not configured.
    /// </summary>
    public async Task DeleteRawArtifactsAsync(Guid runId, CancellationToken ct = default)
    {
        if (!IsConfigured) return;

        var prefix = $"artifacts-raw/{runId:N}/";
        using var s3 = CreateClient();

        string? continuationToken = null;
        do
        {
            var listResponse = await s3.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix,
                ContinuationToken = continuationToken,
            }, ct);

            if (listResponse.S3Objects.Count > 0)
            {
                await RetryS3Async(() => s3.DeleteObjectsAsync(new DeleteObjectsRequest
                {
                    BucketName = _bucketName,
                    Objects = listResponse.S3Objects.Select(o => new KeyVersion { Key = o.Key }).ToList(),
                }, ct), ct);
            }

            continuationToken = listResponse.IsTruncated == true ? listResponse.NextContinuationToken : null;
        } while (continuationToken is not null);
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
    /// Also handles extensionless files stored by act v7+ (direct-upload format): tries to open
    /// them as zip archives and unpack their contents; falls back to including them as raw files.
    /// </summary>
    private static async Task ZipDirectoryAsync(string sourceDir, Stream destination, CancellationToken ct)
    {
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);

        foreach (var filePath in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();

            var ext = Path.GetExtension(filePath);
            // act stores artifact files as individual zip archives. Re-package their contents
            // directly so the resulting download archive is a clean, single-level ZIP.
            // Also try extensionless files (act v7+ direct-upload format) as potential zip archives.
            if (string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(ext))
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
