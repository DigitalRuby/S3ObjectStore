namespace DigitalRuby.S3ObjectStore.Tests
{
    /// <summary>
    /// Storage service tests
    /// </summary>
    [TestFixture]
    public sealed class StorageServiceTests : Microsoft.Extensions.Internal.ISystemClock
    {
        private sealed class FakeObject : IStorageObject
        {
            public string? Key { get; set; }
            public string? Owner { get; set; }
            public string? Value { get; set; }
        }

        private const string bucket = "test";
        
        /// <inheritdoc />
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
        
        private S3StorageMemoryRepository repository;
        
        [SetUp]
        public void Setup()
        {
            repository = new(new FakeTimeProvider());
            repository.CreateBucketAsync(bucket).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Test object service
        /// </summary>
        /// <returns>Task</returns>
        [Test]
        public async Task TestAll()
        {
            var service = CreateService("users/{0}/sessions");
            var fakeObj = new FakeObject { Key = "123", Owner = "me", Value = "abc" };
            await service.SetObjectAsync(fakeObj);
            
            // we own this object
            var obj = await service.GetObjectAsync("123", "me");
            Assert.That(obj, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(obj.Key, Is.EqualTo("123"));
                Assert.That(obj.Owner, Is.EqualTo("me"));
                Assert.That(obj.Value, Is.EqualTo("abc"));
            });

            var objs = await service.GetObjectsAsync("me");
            Assert.That(objs, Has.Count.EqualTo(1));

            // we don't own this object
            objs = await service.GetObjectsAsync("notme");
            Assert.That(objs, Has.Count.EqualTo(0));
            obj = await service.GetObjectAsync("123", "notme");
            Assert.That(obj, Is.Null);

            obj = await service.GetObjectAsync("notfound", "doesn't matter");
            Assert.That(obj, Is.Null);

            fakeObj.Key = null;
            service = CreateService("users/{0}/profile.json", true);
            await service.SetObjectAsync(fakeObj);
            
            obj = await service.GetObjectAsync("123", "me");
            Assert.That(obj, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(obj.Key, Is.Null);
                Assert.That(obj.Owner, Is.EqualTo("me"));
                Assert.That(obj.Value, Is.EqualTo("abc"));
            });

            // key doesn't matter in this case
            obj = await service.GetObjectAsync(null, "me");
            Assert.That(obj, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(obj.Key, Is.Null);
                Assert.That(obj.Owner, Is.EqualTo("me"));
                Assert.That(obj.Value, Is.EqualTo("abc"));
            });

            // we don't own this object
            obj = await service.GetObjectAsync("123", "notme");
            Assert.That(obj, Is.Null);

            obj = await service.GetObjectAsync("notfound", "doesn't matter");
            Assert.That(obj, Is.Null);
        }

        private S3StorageObjectService<FakeObject> CreateService(string folderFormat, bool folderFormatIncludesFileName = false)
        {
            return new S3StorageObjectService<FakeObject>(new StorageObjectServiceOptions<FakeObject>
            {
                Bucket = bucket,
                FolderFormat = folderFormat,
                FolderFormatIncludesFileName = folderFormatIncludesFileName
            }, repository);
        }
    }
}
