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
    private readonly IStorageRepository repository;
    
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="options">Options</param>
    /// <param name="repository">S3 repository</param>
    public S3StorageObjectService(StorageObjectServiceOptions<T> options, IStorageRepository repository)
    {
        this.options = options;
        this.repository = repository;
        if (!options.FolderFormatIncludesFileName)
        {
            options.FolderFormat = options.FolderFormat.Trim('/') + "/";
        }
    }

    /// <inheritdoc />
    public async Task<T?> GetObjectAsync(string key, string owner)
    {
        var path = options.FormatFilePath(key, owner);
        using var result = await repository.ReadAsync(options.Bucket, path);
        if (result is not null)
        {
            var obj = System.Text.Json.JsonSerializer.Deserialize<T>(result);
            return obj;
        }
        return null;
    }

    /// <inheritdoc />
    public async Task SetObjectAsync(T obj)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(obj, jsonOptions);
        using var jsonStream = new System.IO.MemoryStream(json);

        // must await because of using statement on stream above
        var path = options.FormatFilePath(obj.Key, obj.Owner);
        await repository.UpsertAsync(options.Bucket, path, "application/json", jsonStream);
    }
    
    /// <inheritdoc />
    public async Task<IReadOnlyCollection<T>> GetObjectsAsync(string owner)
    {
        var path = options.FormatFolderPath(owner);
        var result = await repository.ListBucketContentsAsync(options.Bucket, path);
        var objects = new List<T>(result.Count);
        List<Task<T?>> tasks = new(result.Count);
        foreach (var item in result)
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
    public Task<IReadOnlyCollection<string>> GetKeys(string owner)
    {
        var path = options.FormatFolderPath(owner);
        return repository.ListBucketContentsAsync(options.Bucket, path)
            .ContinueWith(t =>
            {
                var result = t.Result;
                return result.Select(i => i.Key).ToArray() as IReadOnlyCollection<string>;
            });
    }

    /// <inheritdoc />
    public Task DeleteObjectAsync(string key, string owner)
    {
        var path = options.FormatFilePath(key, owner);
        return repository.DeleteAsync(options.Bucket, path);
    }

    private async Task<T?> GetObjectRawAsync(string rawKey)
    {
        var json = await repository.ReadAsync(options.Bucket, rawKey);
        if (json is null)
        {
            return null;
        }
        var obj = System.Text.Json.JsonSerializer.Deserialize<T>(json);
        return obj;
    }
}
