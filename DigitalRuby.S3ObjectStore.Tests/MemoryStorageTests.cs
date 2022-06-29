namespace DigitalRuby.S3ObjectStore.Tests;

/// <summary>
/// Memory storage tests
/// </summary>
[TestFixture]
public sealed class MemoryStorageTests : Microsoft.Extensions.Internal.ISystemClock
{
    private const string bucket = "test";
    private const string json = "{\"key\":\"value\",\"key2\":42}";
    
    private static readonly MemoryStream item = new(Encoding.UTF8.GetBytes(json));
        
    private S3StorageMemoryRepository repository;

    /// <inheritdoc />
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Setup
    /// </summary>
    [SetUp]
    public void Setup()
    {
        repository = new(this);
        repository.CreateBucketAsync(bucket, default).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Test no exception if no bucket
    /// </summary>
    /// <returns>Task</returns>
    [Test]
    public async Task TestNoBucket()
    {
        await repository.ReadAsync("nobucket", "key", default);
    }

    /// <summary>
    /// Test get bucket
    /// </summary>
    /// <returns>Task</returns>
    [Test]
    public async Task TestGetBucket()
    {
        var buckets = await repository.ListBucketsAsync();
        Assert.That(buckets, Has.Count.EqualTo(1));

        await repository.DeleteBucketAsync(bucket);
        buckets = await repository.ListBucketsAsync();
        Assert.That(buckets, Is.Empty);
    }
    
    /// <summary>
    /// Test add remove items
    /// </summary>
    /// <returns>Task</returns>
    [Test]
    public async Task TestAddRemove()
    {
        await repository.DeleteAsync(bucket, "nofile");
        await repository.UpsertAsync(bucket, "file", "application/json", item);
        var json = await ReadJsonAsync();
        Assert.That(json, Is.EqualTo(MemoryStorageTests.json));
    }

    /// <summary>
    /// Test list contents
    /// </summary>
    /// <returns>Task</returns>
    [Test]
    public async Task TestListContents()
    {
        await repository.UpsertAsync(bucket, "file1", "application/json", item);
        await repository.UpsertAsync(bucket, "file2", "application/json", item);
        await repository.UpsertAsync(bucket, "file3", "application/json", item);
        await repository.UpsertAsync(bucket, "file4", "application/json", item);
        await repository.UpsertAsync(bucket, "other1", "application/json", item);
        await repository.UpsertAsync(bucket, "other2", "application/json", item);
        
        var items = await repository.ListBucketContentsAsync(bucket, "file");
        Assert.That(items, Has.Count.EqualTo(4));

        items = await repository.ListBucketContentsAsync(bucket, "other");
        Assert.That(items, Has.Count.EqualTo(2));
    }

    private async Task<string?> ReadJsonAsync()
    {
        var stream = await repository.ReadAsync(bucket, "file");
        if (stream is null)
        {
            return null;
        }
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }
}