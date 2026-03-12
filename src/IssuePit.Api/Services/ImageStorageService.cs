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
            AutoCloseStream = false, // The caller manages the stream lifetime; don't close it after upload
        };

        await RetryS3Async(() =>
        {
            if (content.CanSeek) content.Position = 0; // Reset stream for each retry attempt
            return s3.PutObjectAsync(request, ct);
        }, ct);

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

        await RetryS3Async(() =>
        {
            if (content.CanSeek) content.Position = 0; // Reset stream for each retry attempt
            return s3.PutObjectAsync(request, ct);
        }, ct);

        return BuildPublicUrl(key);
    }

    /// <summary>
    /// Returns true when the storage service is configured (i.e. an S3 service URL is set).
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_opts.ServiceUrl);

    /// <summary>
    /// Opens the S3 object at <paramref name="key"/> for reading and returns the stream together
    /// with the reported content type. The caller is responsible for disposing the returned stream.
    /// Throws <see cref="FileNotFoundException"/> when the key does not exist in the bucket.
    /// </summary>
    public async Task<(Stream Stream, string ContentType)> OpenDownloadStreamAsync(string key, CancellationToken ct = default)
    {
        var s3 = CreateClient();
        try
        {
            GetObjectResponse response;
            try
            {
                response = await s3.GetObjectAsync(new GetObjectRequest
                {
                    BucketName = _opts.BucketName,
                    Key = key,
                }, ct);
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchKey")
            {
                s3.Dispose();
                throw new FileNotFoundException($"Artifact key '{key}' not found in S3 bucket '{_opts.BucketName}'.", ex);
            }

            // Wrap the response stream so the S3 client is disposed when the stream is closed.
            var contentType = response.Headers.ContentType ?? "application/octet-stream";
            var wrapped = new CompositeDisposeStream(response.ResponseStream, s3, response);
            return (wrapped, contentType);
        }
        catch
        {
            s3.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Wraps a stream and disposes additional resources when the stream is closed.
    /// </summary>
    private sealed class CompositeDisposeStream(Stream inner, params IDisposable[] disposables) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
            inner.ReadAsync(buffer, offset, count, ct);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) =>
            inner.ReadAsync(buffer, ct);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
                foreach (var d in disposables) d.Dispose();
            }
            base.Dispose(disposing);
        }
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
                    ObjectOwnership = ObjectOwnership.ObjectWriter, // Allow CannedACL on objects (LocalStack 4.x and AWS S3 default to BucketOwnerEnforced which disables ACLs)
                }, ct);
                logger.LogInformation("Created S3 bucket '{Bucket}'", _opts.BucketName);
                return;
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
            {
                // Bucket already exists — nothing to do
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

    /// <summary>Retries an S3 operation up to 3 times on transient failures.</summary>
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

        // Default AWS S3 path-style URL
        return $"https://s3.{_opts.Region}.amazonaws.com/{_opts.BucketName}/{key}";
    }
}
