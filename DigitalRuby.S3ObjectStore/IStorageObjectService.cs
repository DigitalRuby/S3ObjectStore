namespace DigitalRuby.S3ObjectStore;

/// <summary>
/// Storage object service options
/// </summary>
public sealed class StorageObjectServiceOptions<T> where T : class, IStorageObject
{
    /// <summary>
    /// Bucket
    /// </summary>
    public string Bucket { get; set; } = string.Empty;

    /// <summary>
    /// Folder format example: users/{0}/sessions<br/>.
    /// The {0} is not required.<br/>
    /// {0} is the owner.<br/>
    /// </summary>
    public string FolderFormat { get; set; } = string.Empty;

    /// <summary>
    /// Whether the folder format also includes the file name, if so the key will not be used to create a file name
    /// </summary>
    public bool FolderFormatIncludesFileName { get; set; }
    
    /// <summary>
    /// Format the folder path with an owner
    /// </summary>
    /// <param name="owner">Owner</param>
    /// <returns>Folder path</returns>
    public string FormatFolderPath(string? owner)
    {
        return string.Format(FolderFormat, owner);
    }

    /// <summary>
    /// Format a file path
    /// </summary>
    /// <param name="key">Key</param>
    /// <param name="owner">Owner</param>
    /// <returns>File path</returns>
    public string FormatFilePath(string key, string? owner)
    {
        return FormatFolderPath(owner) +
            (FolderFormatIncludesFileName ? string.Empty : key + ".json");
    }
}

/// <summary>
/// Storage object interface
/// </summary>
public interface IStorageObject
{
    /// <summary>
    /// Object key
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Owner identifier
    /// </summary>
    string? Owner { get; }
}

/// <summary>
/// Storage object service interface. Stores one or more objects (like sessions) with an owner (like a user).
/// </summary>
/// <typeparam name="T">Types of objects to work with, must be json serializable</typeparam>
public interface IStorageObjectService<T> where T : class, IStorageObject
{
    /// <summary>
    /// Get an object by key and owner
    /// </summary>
    /// <param name="key">Key</param>
    /// <param name="owner">Owner identifier</param>
    /// <returns>Object or null if not found</returns>
    Task<T?> GetObjectAsync(string key, string owner);

    /// <summary>
    /// Set an object. The key and owner properties are used to determine the folder path
    /// </summary>
    /// <param name="obj">Object</param>
    /// <returns>Task</returns>
    Task SetObjectAsync(T obj);

    /// <summary>
    /// Get all objects for the owner.
    /// </summary>
    /// <param name="owner">Owner identifier</param>
    /// <returns>Task of found objects</returns>
    Task<IReadOnlyCollection<T>> GetObjectsAsync(string owner);

    /// <summary>
    /// Get just the keys for the owner, much more lightweight operation
    /// </summary>
    /// <param name="owner">Owner</param>
    /// <returns>Task of found keys</returns>
    Task<IReadOnlyCollection<string>> GetKeys(string owner);

    /// <summary>
    /// Delete object.
    /// </summary>
    /// <param name="key">Key</param>
    /// <param name="owner">Owner identifier</param>
    /// <returns>Task</returns>
    Task DeleteObjectAsync(string key, string owner);
}
