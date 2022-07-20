using System.Linq;

using Microsoft.Extensions.Internal;

namespace DigitalRuby.S3ObjectStore;

/// <summary>
/// Storage repository for local testing all in memory. Not all features are implemented like etags.
/// </summary>
public class S3StorageMemoryRepository : IStorageRepository
{
    private class S3MemoryBucket
    {
        /// <summary>
        /// Date created
        /// </summary>
        public DateTime DateCreated { get; init; }
        
        /// <summary>
        /// Items
        /// </summary>
        public Dictionary<string, S3MemoryObject> Items { get; } = new();
    }
    
    private class S3MemoryObject
    {
        /// <summary>
        /// Data
        /// </summary>
        public byte[] Data { get; set; } = Array.Empty<byte>();
        
        /// <summary>
        /// Last modified
        /// /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Expiration
        /// </summary>
        public DateTime Expires { get; set; } = DateTime.MaxValue;

        /// <summary>
        /// Content type
        /// </summary>
        public string ContentType { get; set; } = string.Empty;
    }

	private readonly Dictionary<string, S3MemoryBucket> buckets = new();
    private readonly ISystemClock clock;
    
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="clock">Clock</param>
    public S3StorageMemoryRepository(ISystemClock clock)
    {
        this.clock = clock;
    }
    
    /// <inheritdoc />
    public Task CreateBucketAsync(string bucket, CancellationToken cancelToken = default)
    {
        lock (buckets)
        {
            if (!buckets.ContainsKey(bucket))
            {
                buckets[bucket] = new() { DateCreated = clock.UtcNow.DateTime };
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(string bucket, string fileName, CancellationToken cancelToken = default)
    {
        lock (buckets)
        {
            if (buckets.TryGetValue(bucket, out var bucketData))
            {
                bucketData.Items.Remove(fileName);
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteBucketAsync(string bucket, CancellationToken cancelToken = default)
    {
        lock (buckets)
        {
            buckets.Remove(bucket);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteObjectsAsync(string bucket, IEnumerable<KeyVersion> files, CancellationToken cancelToken = default)
    {
        lock (buckets)
        {
            if (buckets.TryGetValue(bucket, out var bucketData))
            {
                foreach (var file in files)
                {
                    bucketData.Items.Remove(file.Key);
                }
            }
        }
        return Task.CompletedTask;
    }
    
    /// <inheritdoc />
    public Task<GetObjectMetadataResponse?> GetObjectMetaDataAsync(string bucket, string fileName, CancellationToken cancelToken = default)
    {
        lock (buckets)
        {
            if (buckets.TryGetValue(bucket, out var bucketData) &&
                bucketData.Items.TryGetValue(fileName, out var fileData))
            {
                var metadata = new GetObjectMetadataResponse
                {
                    LastModified = fileData.LastModified,
                    ContentLength = fileData.Data.Length,
                    Expires = fileData.Expires,
                    HttpStatusCode = System.Net.HttpStatusCode.OK,
                };
                metadata.Headers["Content-Type"] = fileData.ContentType;
                return Task.FromResult<GetObjectMetadataResponse?>(metadata);
            }
        }
        return Task.FromResult<GetObjectMetadataResponse?>(null);
    }

    /// <inheritdoc />
    public Task<ListBucketContentsResponse> ListBucketContentsAsync(string bucket, string? prefix = null, string? startAfter = null, string? continuationToken = null, int maxKeys = 1000, CancellationToken cancelToken = default)
    {
        List<S3Object> results = new();
        lock (buckets)
        {
            if (buckets.TryGetValue(bucket, out var bucketData))
            {
                foreach (var kv in bucketData.Items
                    .Where(kv => prefix is null || kv.Key.StartsWith(prefix))
                    .OrderBy(kv => kv.Key))
                {
                    results.Add(new S3Object
                    {
                        BucketName = bucket,
                        Key = kv.Key,
                        LastModified = kv.Value.LastModified,
                        Size = kv.Value.Data.Length
                    });
                }
            }
        }
        return Task.FromResult(new ListBucketContentsResponse(results, null));
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<S3Bucket>> ListBucketsAsync(CancellationToken cancelToken = default)
    {
        List<S3Bucket> results = new();
        lock (buckets)
        {
            foreach (var kv in buckets)
            {
                results.Add(new S3Bucket
                {
                    BucketName = kv.Key,
                    CreationDate = kv.Value.DateCreated
                });
            }
        }
        return Task.FromResult<IReadOnlyCollection<S3Bucket>>(results);
    }

    /// <inheritdoc />
    public Task<Stream?> ReadAsync(string bucket, string fileName, CancellationToken cancelToken = default)
    {
        lock (buckets)
        {
            if (buckets.TryGetValue(bucket, out var bucketData) &&
                bucketData.Items.TryGetValue(fileName, out var fileData))
            {
                return Task.FromResult<Stream?>(new MemoryStream(fileData.Data));
            }
        }
        return Task.FromResult<Stream?>(null);
    }

    /// <inheritdoc />
    public Task UpsertAsync(string bucket, string fileName, string contentType, Stream data, Action<StreamTransferProgressArgs>? progress = null, CancellationToken cancelToken = default)
    {
        lock (buckets)
        {
            if (buckets.TryGetValue(bucket, out var bucketData))
            {
                var ms = new MemoryStream();
                if (data.CanSeek)
                {
                    data.Position = 0;
                }
                data.CopyTo(ms);
                bucketData.Items[fileName] = new S3MemoryObject
                {
                    Data = ms.ToArray(),
                    LastModified = clock.UtcNow.DateTime,
                    ContentType = contentType,
                    Expires = clock.UtcNow.DateTime.AddDays(1)
                };
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<bool> TryDeleteAsync(string bucket, string fileName, CancellationToken cancelToken = default)
    {
        await DeleteAsync(bucket, fileName, cancelToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> TryDeleteObjectsAsync(string bucket, IEnumerable<KeyVersion> files, CancellationToken cancelToken = default)
    {
        await DeleteObjectsAsync(bucket, files, cancelToken);
        return true;
    }

    /// <inheritdoc />
    public Task<Stream?> TryReadAsync(string bucket, string fileName, CancellationToken cancelToken = default)
    {
       return ReadAsync(bucket, fileName, cancelToken);
    }

    /// <inheritdoc />
    public Task<GetObjectMetadataResponse?> TryGetObjectMetaDataAsync(string bucket, string fileName, CancellationToken cancelToken = default)
    {
        return GetObjectMetaDataAsync(bucket, fileName, cancelToken);
    }
}
