using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Options;

namespace Geopilot.Api.Services;

/// <summary>
/// S3-compatible object storage implementation of <see cref="IUploadStorage"/>.
/// As a hosted service it creates the bucket and sets its CORS rules at startup, where configured.
/// </summary>
public sealed class S3UploadStorage : IUploadStorage, IHostedService, IDisposable
{
    private readonly AmazonS3Client client;
    private readonly TransferUtility transferUtility;
    private readonly UploadCloudOptions cloudOptions;
    private readonly Protocol uploadUrlProtocol;
    private readonly ILogger<S3UploadStorage> logger;

    /// <inheritdoc/>
    public string StorageLocation { get; }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> UploadHeaders { get; } = new Dictionary<string, string>();

    /// <summary>
    /// Initializes a new instance of the <see cref="S3UploadStorage"/> class.
    /// </summary>
    public S3UploadStorage(IOptions<UploadCloudOptions> cloudOptions, IOptions<UploadS3Options> s3Options, ILogger<S3UploadStorage> logger)
    {
        ArgumentNullException.ThrowIfNull(cloudOptions);
        ArgumentNullException.ThrowIfNull(s3Options);
        this.logger = logger;
        this.cloudOptions = cloudOptions.Value;

        var s3 = s3Options.Value;
        var serviceUri = new Uri(s3.ServiceUrl);

        // The SDK signs presigned URLs for HTTPS unless told otherwise, also for an HTTP endpoint.
        uploadUrlProtocol = serviceUri.Scheme == Uri.UriSchemeHttp ? Protocol.HTTP : Protocol.HTTPS;

        client = new AmazonS3Client(
            new BasicAWSCredentials(s3.AccessKeyId, s3.SecretAccessKey),
            new AmazonS3Config
            {
                ServiceURL = s3.ServiceUrl,
                AuthenticationRegion = s3.Region,
                ForcePathStyle = s3.ForcePathStyle,
            });
        transferUtility = new TransferUtility(client);

        StorageLocation = $"{serviceUri.GetLeftPart(UriPartial.Path).TrimEnd('/')}/{this.cloudOptions.BucketName}";
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (cloudOptions.AutoCreateContainer)
            await CreateBucketIfMissingAsync(cancellationToken);

        if (cloudOptions.AllowedOrigins.Count > 0)
            await SetCorsRuleAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task<string> GenerateUploadUrlAsync(string key, string? contentType, TimeSpan expiresIn)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = cloudOptions.BucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiresIn),
            Protocol = uploadUrlProtocol,
            ContentType = contentType,
        };

        return await client.GetPreSignedURLAsync(request);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<(string Key, long Size, DateTime LastModified)>> ListFilesAsync(string prefix)
    {
        var results = new List<(string Key, long Size, DateTime LastModified)>();

        await foreach (var s3Object in ListObjects(prefix).S3Objects)
        {
            results.Add((s3Object.Key, s3Object.Size ?? 0, s3Object.LastModified?.ToUniversalTime() ?? DateTime.MinValue));
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task DownloadAsync(string key, Stream destination, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Downloading object {Key}.", key);
        await using var source = await transferUtility.OpenStreamAsync(cloudOptions.BucketName, key, cancellationToken);
        await source.CopyToAsync(destination, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Opening read stream for object {Key}.", key);
        return await transferUtility.OpenStreamAsync(cloudOptions.BucketName, key, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string key)
    {
        await client.DeleteObjectAsync(cloudOptions.BucketName, key);
    }

    /// <inheritdoc/>
    public async Task DeletePrefixAsync(string prefix)
    {
        logger.LogInformation("Deleting objects with prefix {Prefix}.", prefix);

        // One list page holds at most 1000 keys, which is also the limit of one DeleteObjects request.
        await foreach (var page in ListObjects(prefix).Responses)
        {
            if (page.S3Objects is not { Count: > 0 } objects)
                continue;

            await client.DeleteObjectsAsync(new DeleteObjectsRequest
            {
                BucketName = cloudOptions.BucketName,
                Objects = [.. objects.Select(o => new KeyVersion { Key = o.Key })],
            });
        }
    }

    /// <inheritdoc/>
    public async Task<long> GetTotalSizeAsync(string prefix)
    {
        long total = 0;

        await foreach (var s3Object in ListObjects(prefix).S3Objects)
        {
            total += s3Object.Size ?? 0;
        }

        return total;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        transferUtility.Dispose();
        client.Dispose();
    }

    private IListObjectsV2Paginator ListObjects(string prefix)
        => client.Paginators.ListObjectsV2(new ListObjectsV2Request { BucketName = cloudOptions.BucketName, Prefix = prefix });

    private async Task CreateBucketIfMissingAsync(CancellationToken cancellationToken)
    {
        try
        {
            await client.PutBucketAsync(new PutBucketRequest { BucketName = cloudOptions.BucketName }, cancellationToken);
            logger.LogInformation("Created bucket {Bucket}.", cloudOptions.BucketName);
        }
        catch (AmazonS3Exception ex) when (string.Equals(ex.ErrorCode, "BucketAlreadyOwnedByYou", StringComparison.Ordinal))
        {
            logger.LogDebug("Bucket {Bucket} exists already.", cloudOptions.BucketName);
        }
    }

    private async Task SetCorsRuleAsync(CancellationToken cancellationToken)
    {
        var corsRule = new CORSRule
        {
            AllowedOrigins = [.. cloudOptions.AllowedOrigins],
            AllowedMethods = ["GET", "PUT"],
            AllowedHeaders = ["*"],
            ExposeHeaders = ["ETag"],
            MaxAgeSeconds = 3600,
        };

        await client.PutCORSConfigurationAsync(
            new PutCORSConfigurationRequest
            {
                BucketName = cloudOptions.BucketName,
                Configuration = new CORSConfiguration { Rules = [corsRule] },
            },
            cancellationToken);
    }
}
