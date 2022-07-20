namespace DigitalRuby.S3ObjectStore;

/// <summary>
/// Interface for storage access
/// </summary>
public interface IStorageRepository
{
    /// <summary>
    /// Insert or update data
    /// </summary>
    /// <param name="bucket">Bucket name</param>
    /// <param name="fileName">File name</param>
    /// <param name="contentType">Content mime type</param>
    /// <param name="data">Data stream to upload</param>
    /// <param name="progress">Optional progress handler</param>
    /// <param name="cancelToken">Cancel token</param>
    /// <returns>Task</returns>
    Task UpsertAsync(string bucket,
        string fileName,
        string contentType,
        Stream data,
        Action<StreamTransferProgressArgs>? progress = null,
        CancellationToken cancelToken = default);

    /// <summary>
    /// Insert or update data
    /// </summary>
    /// <param name="bucket">Bucket name</param>
    /// <param name="fileName">File name</param>
    /// <param name="contentType">Content mime type</param>
    /// <param name="data">Data bytes to upload</param>
    /// <param name="progress">Optional progress handler</param>
    /// <param name="cancelToken">Cancel token</param>
    /// <returns>Task</returns>
    Task UpsertAsync(string bucket,
        string fileName,
        string contentType,
        byte[] data,
        Action<StreamTransferProgressArgs>? progress = null,
        CancellationToken cancelToken = default) =>
            UpsertAsync(bucket, fileName, contentType, new MemoryStream(data), progress, cancelToken);

    /// <summary>
    /// Insert or update string data
    /// </summary>
    /// <param name="bucket">Bucket name</param>
    /// <param name="fileName">File name</param>
    /// <param name="contentType">Content mime type</param>
    /// <param name="data">Data to upload</param>
    /// <param name="progress">Optional progress handler</param>
    /// <param name="cancelToken">Cancel token</param>
    /// <returns>Task</returns>
    Task UpsertStringAsync(string bucket,
        string fileName,
        string contentType = "application/json",
        string data = "",
        Action<StreamTransferProgressArgs>? progress = null,
        CancellationToken cancelToken = default) =>
            UpsertAsync(bucket, fileName, contentType, new MemoryStream(Encoding.UTF8.GetBytes(data)), progress, cancelToken);

    /// <summary>
    /// Delete data
    /// </summary>
    /// <param name="bucket">Bucket name</param>
    /// <param name="fileName">File name</param>
    /// <param name="cancelToken">Cancel token</param>
    /// <returns>Task</returns>
    Task DeleteAsync(string bucket,
        string fileName,
        CancellationToken cancelToken = default);

    /// <summary>
    /// Deletes a range of objects from S3.  Note that AWS imposes a limit of 1000 keys per batch.
    /// </summary>
    /// <param name="bucket"></param>
    /// <param name="fileNames"></param>
    /// <param name="cancelToken"></param>
    /// <returns>Task</returns>
    Task DeleteObjectsAsync(string bucket,
        IEnumerable<string> fileNames,
        CancellationToken cancelToken = default);

    /// <summary>
    /// Attempt to delete data without exceptions
    /// </summary>
    /// <param name="bucket">Bucket name</param>
    /// <param name="fileName">File name</param>
    /// <param name="cancelToken">Cancel token</param>
    /// <returns>Task of bool success</returns>
    Task<bool> TryDeleteAsync(string bucket,
        string fileName,
        CancellationToken cancelToken = default);

    /// <summary>
    /// Attempts to delete a range of objects.  Note that AWS imposes a limit of 1000 keys per batch.
    /// </summary>
    /// <param name="bucket">Bucket</param>
    /// <param name="fileNames">File names</param>
    /// <param name="cancelToken">Cancel token</param>
    /// <returns>Task of bool if deletion succeeded</returns>
    Task<bool> TryDeleteObjectsAsync(string bucket,
        IEnumerable<string> fileNames,
        CancellationToken cancelToken = default);

    /// <summary>
    /// Read data. You must dispose the returned stream when done with it to avoid leaking handles.
    /// </summary>
    /// <param name="bucket">Bucket name</param>
    /// <param name="fileName">File name</param>
    /// <param name="cancelToken">Cancel token</param>
    /// <returns>Task of stream to read data from</returns>
    Task<Stream?> ReadAsync(string bucket,
        string fileName,
        CancellationToken cancelToken = default);

    /// <summary>
    /// Read string data
    /// </summary>
    /// <param name="bucket">Bucket name</param>
    /// <param name="fileName">File name</param>
    /// <param name="cancelToken">Cancel token</param>
    /// <returns>Task of string data (null if not found)</returns>
    async Task<string?> ReadStringAsync(string bucket,
        string fileName,
        CancellationToken cancelToken = default)
    {
        var stream = await ReadAsync(bucket, fileName, cancelToken);
        if (stream is null)
        {
            return null;
        }
        try
        {
            return await new StreamReader(stream, Encoding.UTF8).ReadToEndAsync();
        }
        finally
        {
            stream.Close();
        }
    }

    /// <summary>
    /// Read data without exceptions. If returned stream is not null, you must dispose the returned stream when done with it to avoid leaking handles.
    /// </summary>
    /// <param name="bucket">Bucket</param>
    /// <param name="fileName">File name</param>
    /// <param name="cancelToken">Cancel token</param>
    /// <returns>Task of stream to read data from or null stream if failure or file does not exist</returns>
    Task<Stream?> TryReadAsync(string bucket,
        string fileName,
        CancellationToken cancelToken = default);

    /// <summary>
    /// Get an object's metadata. (useful when you need to examine read/write times without downloading the entire document)
    /// </summary>
    /// <param name="bucket">Bucket</param>
    /// <param name="fileName">File name</param>
    /// <param name="cancelToken">Cancel token</param>
    /// <returns>Task of metadata response or null if no metadata found</returns>
    Task<GetObjectMetadataResponse?> GetObjectMetaDataAsync(string bucket,
        string fileName,
        CancellationToken cancelToken = default);

    /// <summary>
    /// Try and get an object's metadata. Null if the object cannot be read. (useful when you need to examine read/write times without downloading the entire document)
    /// </summary>
    /// <param name="bucket">Bucket</param>
    /// <param name="fileName">File name</param>
    /// <param name="cancelToken">Cancel token</param>
    /// <returns>Task of metadata response or null if none found</returns>
    Task<GetObjectMetadataResponse?> TryGetObjectMetaDataAsync(string bucket, string fileName, CancellationToken cancelToken = default);

    /// <summary>
    /// Read string data without exceptions
    /// </summary>
    /// <param name="bucket">Bucket</param>
    /// <param name="fileName">File name</param>
    /// <param name="cancelToken">Cancel token</param>
    /// <returns>Task of string or null if failure or file does not exist</returns>
    async Task<string?> TryReadStringAsync(string bucket, string fileName, CancellationToken cancelToken = default)
    {
        Stream? stream = await TryReadAsync(bucket, fileName, cancelToken);
        if (stream is null)
        {
            return null;
        }
        try
        {
            return await new StreamReader(stream, Encoding.UTF8).ReadToEndAsync();
        }
        finally
        {
            stream.Dispose();
        }
    }

    /// <summary>
    /// Create a bucket
    /// </summary>
    /// <param name="bucket">Bucket</param>
    /// <param name="cancelToken">Cancel token</param>
    /// <returns>Task</returns>
    Task CreateBucketAsync(string bucket,
        CancellationToken cancelToken = default);

    /// <summary>
    /// Delete a bucket
    /// </summary>
    /// <param name="bucket">Bucket</param>
    /// <param name="cancelToken">Cancel token</param>
    /// <returns>Task</returns>
    Task DeleteBucketAsync(string bucket,
        CancellationToken cancelToken = default);

    /// <summary>
    /// List all the buckets
    /// </summary>
    /// <param name="cancelToken">Cancel token</param>
    /// <returns>Task of buckets</returns>
    Task<IReadOnlyCollection<S3Bucket>> ListBucketsAsync(CancellationToken cancelToken = default);

    /// <summary>
    /// List bucket contents
    /// </summary>
    /// <param name="bucket">Bucket name</param>
    /// <param name="cancelToken">Cancel token</param>
    /// <param name="continuationToken"></param>
    /// <param name="maxKeys"></param>
    /// <param name="prefix"></param>
    /// <returns>Task of bucket contents</returns>
    Task<ListBucketContentsResponse> ListBucketContentsAsync(string bucket,
        string? prefix = null,
        string? continuationToken = null,
        int maxKeys = 1000,
        CancellationToken cancelToken = default);
}

/// <summary>
/// ListBucketResponse response
/// </summary>
/// <param name="Objects">Objects</param>
/// <param name="ContinuationToken">Continuation token</param>
public record ListBucketContentsResponse(IReadOnlyCollection<S3Object> Objects, string? ContinuationToken);