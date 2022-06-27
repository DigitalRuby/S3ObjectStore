using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

using System.Text.Json.Serialization;

namespace DigitalRuby.S3ObjectStore;

/// <summary>
/// Fake hosting environment, not needed when using dependency injection in a full .net core console app or web service
/// </summary>
public sealed class FakeEnvironment : IHostEnvironment, IFileProvider
{
    public FakeEnvironment() { ContentRootFileProvider = this; }
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = System.Reflection.Assembly.GetEntryAssembly()!.GetName().Name!;
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; }
    public IDirectoryContents GetDirectoryContents(string subpath) => throw new NotImplementedException();
    public IFileInfo GetFileInfo(string subpath) => throw new NotImplementedException();
    public IChangeToken Watch(string filter) => throw new NotImplementedException();
}

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

/// <summary>
/// Main class
/// </summary>
public class Program
{
    /// <summary>
    /// Main method
    /// </summary>
    /// <param name="args">Args</param>
    /// <returns>Task</returns>
    public static async Task Main()
    {
        // create a file, s3credentials.txt and it will have 3 lines:
        // access key
        // secret key
        // url
        // ...
        // this file is ignored in the .gitignore
        // traverse down from the bin/debug/net6.0 folder to the credentials file
        string[] lines = System.IO.File.ReadAllLines("../../../s3credentials.txt");

        // access key, secret key, url, disable signing
        // note disable signing is required for cloudflare r2
        // set disable signing to false as long as your s3 provider works
        var config = new S3Config(lines[0], lines[1], lines[2], true);

        // for a full .net application, you can get the hosting environment from your builder object or IServiceProvider
        var repository = new S3StorageRepository(config, new FakeEnvironment(), new NullLogger<S3StorageRepository>());

        // for this example we are storing user sessions, each user (represented by account id key, which goes in folder format {0})
        // you will need to create a bucket called digitalrubys3test
        var serviceOptions = new StorageObjectServiceOptions<Session>
        {
            Bucket = "digitalrubys3test",

            // the folder format does not need a {0} if there is no owner for the object (owner is null)
            // by default the key will be appended to this folder with a .json extension
            FolderFormat = "users/{0}/sessions",

            // if your folder format contains the file name, for example to store a user profile, you could use:
            // users/{0}/profile.json, which would ignore the key as part of the file name
            FolderFormatIncludesFileName = false
        };

        // create s3 object service, wrapping the s3 repository
        var service = new S3StorageObjectService<Session>(serviceOptions, repository);

        // create some guids for testing
        var userId = Guid.Parse("18B73C9B-CF5F-469D-9E61-6679DD88BC76").ToString("N");
        var session1Id = Guid.Parse("DC8184E6-E592-4050-DD2F-607FDE4D6E7F").ToString("N");
        var session1I2 = Guid.Parse("05F9E5A0-97CA-444A-C716-AF069CE4E710").ToString("N");
        
        var session = new Session
        {
            Key = session1Id,
            Owner = userId,
            IPAddress = "2.2.2.2",
            UserAgent = "Mozilla/5.0 (platform; rv:geckoversion) Gecko/geckotrail Firefox/firefoxversion",
            Expires = DateTimeOffset.Now.AddDays(1),
            Permissions = "read,write"
        };
        await service.SetObjectAsync(session);
        // users/{userId}/sessions/{session.Key}.json now exists

        // create another session
        var session2 = new Session
        {
            Key = session1I2,
            Owner = userId,
            IPAddress = "3.3.3.3",
            UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 13_4_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/13.1 Mobile/15E148 Safari/604.1",
            Expires = DateTimeOffset.Now.AddDays(1),
            Permissions = "read,write"
        };
        await service.SetObjectAsync(session2);
        // users/{userId}/sessions/{session2.Key}.json now exists

        // get all the sessions for the user
        var sessions = await service.GetObjectsAsync(userId);
        foreach (var foundSession in sessions)
        {
            Console.WriteLine("Session: {0}", foundSession);
        }

        // clean up after ourselves
        await service.DeleteObjectAsync(session.Key, session.Owner);
        await service.DeleteObjectAsync(session2.Key, session.Owner);
        
        Console.WriteLine("Done!");
        Console.ReadLine();
    }
}