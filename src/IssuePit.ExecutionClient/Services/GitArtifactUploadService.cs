using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace IssuePit.ExecutionClient.Services;

/// <summary>
/// Uploads a git workspace archive (.git folder tar) to S3-compatible storage when an
/// agent push fails, so the work is not lost and can be recovered manually.
///
/// Reads from the same <c>ImageStorage:*</c> configuration keys used by
/// <c>IssuePit.CiCdClient.Services.ArtifactStorageService</c>:
/// <list type="bullet">
///   <item><c>ImageStorage:ServiceUrl</c></item>
///   <item><c>ImageStorage:AccessKey</c></item>
///   <item><c>ImageStorage:SecretKey</c></item>
///   <item><c>ImageStorage:BucketName</c></item>
///   <item><c>ImageStorage:PublicBaseUrl</c></item>
///   <item><c>ImageStorage:Region</c></item>
/// </list>
/// When <c>ImageStorage:ServiceUrl</c> is not configured, uploads are skipped.
/// </summary>
public class GitArtifactUploadService(IConfiguration configuration, ILogger<GitArtifactUploadService> logger)
{
    private readonly string? _serviceUrl = configuration["ImageStorage:ServiceUrl"];
    private readonly string _accessKey = configuration["ImageStorage:AccessKey"] ?? "test";
    private readonly string _secretKey = configuration["ImageStorage:SecretKey"] ?? "test";
    private readonly string _bucketName = configuration["ImageStorage:BucketName"] ?? "issuepit-uploads";
    private readonly string? _publicBaseUrl = configuration["ImageStorage:PublicBaseUrl"];
    private readonly string _region = configuration["ImageStorage:Region"] ?? "us-east-1";

    private volatile bool _bucketEnsured;
    private readonly SemaphoreSlim _bucketLock = new(1, 1);

    /// <summary>Returns <c>true</c> when S3 credentials are configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_serviceUrl);

    /// <summary>
    /// Uploads a tar stream (Docker archive of <c>/workspace/.git</c>) to S3 and returns the public
    /// download URL, or <c>null</c> when storage is not configured.
    /// </summary>
    public async Task<string?> UploadGitArchiveAsync(
        Stream tarStream,
        Guid agentSessionId,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return null;

        var key = $"git-archives/{agentSessionId:N}/workspace-git.tar";

        using var s3 = CreateClient();
        await EnsureBucketAsync(s3, cancellationToken);

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = tarStream,
            ContentType = "application/x-tar",
        };

        await s3.PutObjectAsync(request, cancellationToken);
        var url = BuildPublicUrl(key);
        logger.LogInformation("Uploaded git archive for session {SessionId} to S3: {Url}", agentSessionId, url);
        return url;
    }

    private AmazonS3Client CreateClient()
    {
        var credentials = new BasicAWSCredentials(_accessKey, _secretKey);
        var config = new AmazonS3Config
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_region),
            ForcePathStyle = true,
        };
        if (!string.IsNullOrWhiteSpace(_serviceUrl))
            config.ServiceURL = _serviceUrl;
        return new AmazonS3Client(credentials, config);
    }

    private async Task EnsureBucketAsync(AmazonS3Client s3, CancellationToken cancellationToken)
    {
        if (_bucketEnsured) return;
        await _bucketLock.WaitAsync(cancellationToken);
        try
        {
            if (_bucketEnsured) return;
            var buckets = await s3.ListBucketsAsync(cancellationToken);
            if (!buckets.Buckets.Any(b => b.BucketName == _bucketName))
                await s3.PutBucketAsync(new PutBucketRequest { BucketName = _bucketName }, cancellationToken);
            _bucketEnsured = true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not ensure S3 bucket '{Bucket}' exists", _bucketName);
        }
        finally
        {
            _bucketLock.Release();
        }
    }

    private string BuildPublicUrl(string key)
    {
        if (!string.IsNullOrWhiteSpace(_publicBaseUrl))
            return $"{_publicBaseUrl.TrimEnd('/')}/{key}";
        return $"{_serviceUrl!.TrimEnd('/')}/{_bucketName}/{key}";
    }
}
