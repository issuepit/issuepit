using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace IssuePit.Notes.Api.Services;

public class NotesImageStorageOptions
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

    /// <summary>S3 bucket name for uploaded note images.</summary>
    public string BucketName { get; set; } = "issuepit-notes-uploads";

    /// <summary>
    /// Public base URL used to generate image links returned to clients.
    /// When empty, the URL is derived from the S3 service URL + bucket name.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>AWS region. Defaults to us-east-1 (required by LocalStack and many S3-compatible services).</summary>
    public string Region { get; set; } = "us-east-1";
}

public class NotesImageStorageService(IOptions<NotesImageStorageOptions> options, ILogger<NotesImageStorageService> logger)
{
    private readonly NotesImageStorageOptions _opts = options.Value;
    private volatile bool _bucketEnsured;

    /// <summary>
    /// Returns true when the storage service is configured (i.e. an S3 service URL is set).
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_opts.ServiceUrl);

    private IAmazonS3 CreateClient()
    {
        var credentials = new BasicAWSCredentials(_opts.AccessKey, _opts.SecretKey);
        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(_opts.Region),
            ForcePathStyle = true,
        };

        if (!string.IsNullOrWhiteSpace(_opts.ServiceUrl))
        {
            config.ServiceURL = _opts.ServiceUrl;
        }

        return new AmazonS3Client(credentials, config);
    }

    /// <summary>
    /// Uploads an image and returns its public URL.
    /// </summary>
    public async Task<string> UploadImageAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(fileName);
        if (!string.IsNullOrEmpty(extension))
        {
            extension = new string(extension.Where(c => char.IsLetterOrDigit(c) || c == '.').ToArray());
        }
        var key = $"notes-images/{Guid.NewGuid():N}{extension}";

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
            AutoCloseStream = false,
        };

        await RetryS3Async(() =>
        {
            if (content.CanSeek) content.Position = 0;
            return s3.PutObjectAsync(request, ct);
        }, ct);

        return BuildPublicUrl(key);
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
                    BucketName = _opts.BucketName,
                    UseClientRegion = true,
                    ObjectOwnership = ObjectOwnership.ObjectWriter,
                }, ct);
                logger.LogInformation("Created S3 bucket '{Bucket}'", _opts.BucketName);
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
                logger.LogWarning(ex, "S3 bucket creation attempt {Attempt}/{Max} failed, retrying in {Delay}s", attempt + 1, maxAttempts, delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }
            catch (Exception ex)
            {
                lastEx = ex;
            }
        }
        throw new InvalidOperationException($"Failed to ensure S3 bucket '{_opts.BucketName}' after {maxAttempts} attempts.", lastEx);
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
                logger.LogWarning(ex, "S3 operation attempt {Attempt}/{Max} failed, retrying in {Delay}s", attempt + 1, maxAttempts, delay.TotalSeconds);
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
        if (!string.IsNullOrWhiteSpace(_opts.PublicBaseUrl))
            return $"{_opts.PublicBaseUrl.TrimEnd('/')}/{key}";

        if (!string.IsNullOrWhiteSpace(_opts.ServiceUrl))
            return $"{_opts.ServiceUrl.TrimEnd('/')}/{_opts.BucketName}/{key}";

        return $"https://s3.{_opts.Region}.amazonaws.com/{_opts.BucketName}/{key}";
    }
}
