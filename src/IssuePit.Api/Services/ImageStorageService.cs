using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace IssuePit.Api.Services;

public class ImageStorageOptions
{
    public const string SectionName = "ImageStorage";

    /// <summary>
    /// The S3 service URL. Leave empty to use AWS default.
    /// Set to a LocalStack URL (e.g. http://localhost:4566) for local development,
    /// or to a Backblaze B2 S3-compatible endpoint (e.g. https://s3.us-west-004.backblazeb2.com).
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>AWS access key ID (or B2 key ID).</summary>
    public string AccessKey { get; set; } = "test";

    /// <summary>AWS secret access key (or B2 application key).</summary>
    public string SecretKey { get; set; } = "test";

    /// <summary>S3 bucket name for uploaded images.</summary>
    public string BucketName { get; set; } = "issuepit-uploads";

    /// <summary>
    /// Public base URL used to generate image links returned to clients.
    /// When empty, the URL is derived from the S3 service URL + bucket name.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>AWS region. Defaults to us-east-1 (required by LocalStack and many S3-compatible services).</summary>
    public string Region { get; set; } = "us-east-1";
}

public class ImageStorageService(IOptions<ImageStorageOptions> options, ILogger<ImageStorageService> logger)
{
    private readonly ImageStorageOptions _opts = options.Value;
    private volatile bool _bucketEnsured;

    private IAmazonS3 CreateClient()
    {
        var credentials = new BasicAWSCredentials(_opts.AccessKey, _opts.SecretKey);
        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(_opts.Region),
            ForcePathStyle = true, // Required for LocalStack and B2
        };

        if (!string.IsNullOrWhiteSpace(_opts.ServiceUrl))
        {
            config.ServiceURL = _opts.ServiceUrl;
        }

        return new AmazonS3Client(credentials, config);
    }

    /// <summary>
    /// Uploads any file to the given <paramref name="subfolder"/> and returns its public URL.
    /// </summary>
    public async Task<string> UploadFileAsync(Stream content, string fileName, string contentType, string subfolder, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(fileName);
        if (!string.IsNullOrEmpty(extension))
        {
            extension = new string(extension.Where(c => char.IsLetterOrDigit(c) || c == '.').ToArray());
        }
        var safeSubfolder = new string(subfolder.Where(c => char.IsLetterOrDigit(c) || c == '/').ToArray()).Trim('/');
        var key = $"{safeSubfolder}/{Guid.NewGuid():N}{extension}";

        using var s3 = CreateClient();

        if (!_bucketEnsured)
        {
            await EnsureBucketExistsAsync(s3, ct);
            _bucketEnsured = true;
        }

        var request = new PutObjectRequest
        {
            BucketName = _opts.BucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType,
        };

        await s3.PutObjectAsync(request, ct);

        return BuildPublicUrl(key);
    }

    /// <summary>
    /// Uploads an image and returns its public URL.
    /// </summary>
    public async Task<string> UploadImageAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        // Derive a safe key: use only the original file extension (user-supplied name is not trusted)
        var extension = Path.GetExtension(fileName);
        if (!string.IsNullOrEmpty(extension))
        {
            // Allow only safe extension characters
            extension = new string(extension.Where(c => char.IsLetterOrDigit(c) || c == '.').ToArray());
        }
        var key = $"images/{Guid.NewGuid():N}{extension}";

        using var s3 = CreateClient();

        if (!_bucketEnsured)
        {
            await EnsureBucketExistsAsync(s3, ct);
            _bucketEnsured = true;
        }

        var request = new PutObjectRequest
        {
            BucketName = _opts.BucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            CannedACL = S3CannedACL.PublicRead,
        };

        await s3.PutObjectAsync(request, ct);

        return BuildPublicUrl(key);
    }

    private async Task EnsureBucketExistsAsync(IAmazonS3 s3, CancellationToken ct)
    {
        try
        {
            await s3.PutBucketAsync(new PutBucketRequest
            {
                BucketName = _opts.BucketName,
                UseClientRegion = true,
                ObjectOwnership = ObjectOwnership.ObjectWriter, // Allow CannedACL on objects (LocalStack 4.x and AWS S3 default to BucketOwnerEnforced which disables ACLs)
            }, ct);
            logger.LogInformation("Created S3 bucket '{Bucket}'", _opts.BucketName);
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
        {
            // Bucket already exists — nothing to do
        }
    }

    private string BuildPublicUrl(string key)
    {
        if (!string.IsNullOrWhiteSpace(_opts.PublicBaseUrl))
            return $"{_opts.PublicBaseUrl.TrimEnd('/')}/{key}";

        if (!string.IsNullOrWhiteSpace(_opts.ServiceUrl))
            return $"{_opts.ServiceUrl.TrimEnd('/')}/{_opts.BucketName}/{key}";

        // Default AWS S3 path-style URL
        return $"https://s3.{_opts.Region}.amazonaws.com/{_opts.BucketName}/{key}";
    }
}
