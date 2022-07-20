namespace DigitalRuby.S3ObjectStore;

/// <inheritdoc />
public sealed class S3StorageObjectService<T> : IStorageObjectService<T> where T : class, IStorageObject
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault,
        IgnoreReadOnlyFields = true,
        IgnoreReadOnlyProperties = true
    };
    
    private readonly StorageObjectServiceOptions<T> options;

    /// <inheritdoc />
    public IStorageRepository Repository { get; }
    
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="options">Options</param>
    /// <param name="repository">S3 repository</param>
    public S3StorageObjectService(StorageObjectServiceOptions<T> options, IStorageRepository repository)
    {
        this.options = options;
        Repository = repository;
        if (!options.FolderFormatIncludesFileName)
        {
            options.FolderFormat = options.FolderFormat.Trim('/') + "/";
        }
    }

    /// <inheritdoc />
    public async Task<T?> GetObjectAsync(string? key, string owner, CancellationToken cancelToken = default)
    {
        var path = options.FormatFilePath(key, owner);
        using var result = await Repository.ReadAsync(options.Bucket, path, cancelToken);
        if (result is not null)
        {
            var obj = System.Text.Json.JsonSerializer.Deserialize<T>(result);
            return obj;
        }
        return null;
    }

    /// <inheritdoc />
    public async Task SetObjectAsync(T obj, CancellationToken cancelToken = default)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(obj, jsonOptions);
        using var jsonStream = new System.IO.MemoryStream(json);

        // must await because of using statement on stream above
        var path = options.FormatFilePath(obj.Key, obj.Owner);
        await Repository.UpsertAsync(options.Bucket, path, "application/json", jsonStream, cancelToken: cancelToken);
    }
    
    /// <inheritdoc />
    public async Task<IReadOnlyCollection<T>> GetObjectsAsync(string owner, CancellationToken cancelToken = default)
    {
        var path = options.FormatFolderPath(owner);
        var result = await Repository.ListBucketContentsAsync(options.Bucket, path, cancelToken: cancelToken);
        var objects = new List<T>(result.Objects.Count);
        List<Task<T?>> tasks = new(result.Objects.Count);
        foreach (var item in result.Objects)
        {
            tasks.Add(Task.Run(() => GetObjectRawAsync(item.Key)));
        }
        await Task.WhenAll(tasks);
        foreach (var task in tasks.Where(t => t.Result is not null))
        {
            objects.Add(task.Result!);
        }
        return objects;
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<string>> GetKeys(string owner, CancellationToken cancelToken = default)
    {
        var path = options.FormatFolderPath(owner);
        return Repository.ListBucketContentsAsync(options.Bucket, path, cancelToken: cancelToken)
            .ContinueWith(t =>
            {
                var result = t.Result;
                return result.Objects.Select(i => i.Key).ToArray() as IReadOnlyCollection<string>;
            });
    }

    /// <inheritdoc />
    public Task DeleteObjectAsync(string? key, string owner, CancellationToken cancelToken = default)
    {
        var path = options.FormatFilePath(key, owner);
        return Repository.DeleteAsync(options.Bucket, path, cancelToken);
    }

    private async Task<T?> GetObjectRawAsync(string rawKey, CancellationToken cancelToken = default)
    {
        var json = await Repository.ReadAsync(options.Bucket, rawKey, cancelToken);
        if (json is null)
        {
            return null;
        }
        var obj = System.Text.Json.JsonSerializer.Deserialize<T>(json);
        return obj;
    }
}
