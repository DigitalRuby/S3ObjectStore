# S3 Object Store

## Allow storing json objects in S3 easily

Goals:
- Map objects to a sensible s3 hierarchy
- Use json for fast-ish and flexible serialization and model updates + human readability
- Control the folder template name with options

Please see the Sandbox project, `Program.cs` which shows a simple example of using sessions.

## Usage

#### Define an object that implements the `IStorageObject` interface:

```cs
/// <summary>
/// Example session object
/// </summary>
public sealed class Session : IStorageObject
{
    /// <summary>
    /// The session identifier, probably a guid
    /// </summary>
    [JsonPropertyName("k")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The account id the session belongs to, probably a guid. Can be null if no owner.
    /// </summary>
    [JsonPropertyName("o")]
    public string? Owner { get; set; }

    /// <summary>
    /// IP address
    /// </summary>
    [JsonPropertyName("i")]
    public string IPAddress { get; set; } = string.Empty;

    /// <summary>
    /// User agent
    /// </summary>
    [JsonPropertyName("a")]
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>
    /// When the session expires
    /// </summary>
    [JsonPropertyName("e")]
    public DateTimeOffset Expires { get; set; }

    /// <summary>
    /// Could put permissions for the session here
    /// </summary>
    [JsonPropertyName("p")]
    public string Permissions { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{Key} {Owner} {IPAddress} {UserAgent} {Expires} {Permissions}";
    }
}
```

You must implement the Key, and optionally, owner proeprties.

#### Create your s3 repository

```cs
// note disable signing is required for cloudflare r2
// set disable signing to false as long as your s3 provider works
var config = new S3Config(accessKey, secretKey, url, disableSigning);

// in production, deleting and creating buckets is not allowed for safety
// you can get both the environment and logger from `IServiceProvider` when using a full .net 6 app.
var repository = new S3StorageRepository(config, new FakeEnvironment(), new NullLogger<S3StorageRepository>());
```

#### Create your object service

```cs
var serviceOptions = new StorageObjectServiceOptions<Session>
{
    Bucket = "bucketname",

    // the folder format does not need a {0} if there is no owner for the object (owner is null)
    // by default the key will be appended to this folder with a .json extension
    FolderFormat = "users/{0}/sessions",

    // if your folder format contains the file name, for example to store a user profile, you could use:
    // users/{0}/profile.json, which would ignore the key as part of the file name
    FolderFormatIncludesFileName = false
};

// create s3 object service, wrapping the s3 repository
var service = new S3StorageObjectService<Session>(serviceOptions, repository);
```

#### Perform operations

The storage object service interface is as follows:

```cs
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
    /// <returns>Task of keys</returns>
    Task<IReadOnlyCollection<string>> GetKeys(string owner);

    /// <summary>
    /// Delete object.
    /// </summary>
    /// <param name="key">Key</param>
    /// <param name="owner">Owner identifier</param>
    /// <returns>Task</returns>
    Task DeleteObjectAsync(string key, string owner);
}
```

Please email support@digitalruby.com if you have questions or feedback.

- Jeff
