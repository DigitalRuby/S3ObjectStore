namespace DigitalRuby.S3ObjectStore;

/// <inheritdoc />
public class S3StorageRepository : IStorageRepository
{
	private readonly AmazonS3Client client;
	private readonly bool isProduction;
	private readonly ILogger logger;
	private readonly bool disableSigning;
        
	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="config">Config</param>
	/// <param name="environment">Environment</param>
	/// <param name="logger">Logger</param>
	public S3StorageRepository(S3Config config, IHostEnvironment environment, ILogger<S3StorageRepository> logger)
	{
		var s3Config = new AmazonS3Config
		{
			ServiceURL = config.Url,
			Timeout = TimeSpan.FromSeconds(10.0)
		};
		client = new AmazonS3Client(config.AccessKey, config.SecretKey, s3Config);
		this.isProduction = environment.IsProduction();
		this.logger = logger;
		this.disableSigning = config.DisableSigning;
	}

	/// <inheritdoc />
	public async Task DeleteAsync(string bucket, string fileName, CancellationToken cancelToken = default)
	{
		DeleteObjectRequest request = new()
		{
			BucketName = bucket,
			Key = fileName
		};
		try
		{
			var response = await client.DeleteObjectAsync(request, cancelToken);
			if (response.HttpStatusCode >= System.Net.HttpStatusCode.BadRequest)
			{
				throw new IOException("Failed to delete bucket, status code " + response.HttpStatusCode);
			}
		}
		catch (Amazon.S3.AmazonS3Exception ex)
		{
			if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
			{
				// not fatal
				return;
			}
			throw;
		}
	}

	/// <inheritdoc />
	public async Task DeleteObjectsAsync(string bucket, IEnumerable<string> fileNames, CancellationToken cancelToken = default)
	{
		DeleteObjectsRequest request = new()
		{
			BucketName = bucket,
			Objects = fileNames
				.Select(
					fn =>
						new KeyVersion
						{
							Key = fn
						})
				.ToList()
		};

		var response = await client.DeleteObjectsAsync(request, cancelToken);
		if (response.HttpStatusCode >= System.Net.HttpStatusCode.BadRequest)
		{
			throw new IOException("Failed to delete bucket, status code " + response.HttpStatusCode);
		}
	}

	/// <inheritdoc />
	public async Task<bool> TryDeleteAsync(string bucket, string fileName, CancellationToken cancelToken = default)
	{
		try
		{
			await DeleteAsync(bucket, fileName, cancelToken);
			return true;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to delete bucket data {bucket} {fileName}", bucket, fileName);
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<bool> TryDeleteObjectsAsync(string bucket, IEnumerable<string> fileNames, CancellationToken cancelToken = default)
	{
		try
		{
			await DeleteObjectsAsync(bucket, fileNames, cancelToken);
			return true;
		}
		catch(Exception ex)
		{
			logger.LogError(ex, "Failed to delete bucket objects {bucket} {fileNames}", bucket, fileNames);
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<Stream?> ReadAsync(string bucket, string fileName, CancellationToken cancelToken = default)
	{
		GetObjectRequest request = new()
		{
			BucketName = bucket,
			Key = fileName
		};
		try
		{
			GetObjectResponse response = await client.GetObjectAsync(request, cancelToken);
			if (response.HttpStatusCode >= System.Net.HttpStatusCode.BadRequest)
			{
				throw new IOException("Failed to read bucket, status code " + response.HttpStatusCode);
			}
			return response.ResponseStream;
		}
		catch (Amazon.S3.AmazonS3Exception ex)
		{
			if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
			{
				// not fatal
				return null;
			}
			throw;
		}
	}

	/// <inheritdoc />
	public async Task<Stream?> TryReadAsync(string bucket, string fileName, CancellationToken cancelToken = default)
	{
		try
		{
			return await ReadAsync(bucket, fileName, cancelToken);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to read bucket data {bucket} {fileName}", bucket, fileName);
			return null;
		}
	}

	/// <inheritdoc />
	public async Task<GetObjectMetadataResponse?> GetObjectMetaDataAsync(string bucket, string fileName, CancellationToken cancelToken = default)
	{
		GetObjectMetadataRequest request = new()
		{
			BucketName = bucket,
			Key = fileName
		};

		GetObjectMetadataResponse response = await client.GetObjectMetadataAsync(request, cancelToken);
		if (response.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
		{
			// not fatal
			return null;
		}
		else if (response.HttpStatusCode >= System.Net.HttpStatusCode.BadRequest)
		{
			throw new IOException("Failed to get object metadata, status code " + response.HttpStatusCode);
		}
		return response;
	}

	/// <inheritdoc />
	public async Task<GetObjectMetadataResponse?> TryGetObjectMetaDataAsync(string bucket, string fileName, CancellationToken cancelToken = default)
	{
		try
		{
			return await GetObjectMetaDataAsync(bucket, fileName, cancelToken);
		}
		catch(Exception ex)
		{
			logger.LogError(ex, "Failed to read object metadata for {bucket} {filreName}", bucket, fileName);
			return null;
		}
	}

	/// <inheritdoc />
	public async Task UpsertAsync(string bucket,
		string fileName,
		string contentType,
		Stream data,
		Action<StreamTransferProgressArgs>? progress = null,
		CancellationToken cancelToken = default)
	{
		PutObjectRequest request = new()
		{
			BucketName = bucket,
			Key = fileName,
			ContentType = contentType,
			InputStream = data,
            CalculateContentMD5Header = disableSigning ? false : true,
			DisableMD5Stream = disableSigning,
			DisablePayloadSigning = disableSigning
		};
		if (progress is not null)
		{
			request.StreamTransferProgress = new EventHandler<StreamTransferProgressArgs>((obj, args) =>
			{
				progress.Invoke(args);
			});
		};
		var response = await client.PutObjectAsync(request, cancelToken);
		if (response.HttpStatusCode >= System.Net.HttpStatusCode.BadRequest)
		{
			throw new IOException("Error upserting bucket, status code " + response.HttpStatusCode);
		}
	}

	/// <inheritdoc />
	public async Task CreateBucketAsync(string bucket, CancellationToken cancelToken = default)
	{
		if (isProduction)
		{
			throw new NotSupportedException("Buckets cannot be created programatically in production");
		}

		PutBucketRequest request = new()
		{
			BucketName = bucket
		};
		var response = await client.PutBucketAsync(request, cancelToken);
		if (response.HttpStatusCode >= System.Net.HttpStatusCode.BadRequest)
		{
			throw new IOException("Failed to delete bucket, status code " + response.HttpStatusCode);
		}
	}

	/// <inheritdoc />
	public async Task DeleteBucketAsync(string bucket, CancellationToken cancelToken = default)
	{
		if (isProduction)
		{
			throw new NotSupportedException("Buckets cannot be deleted programatically in production");
		}

		DeleteBucketRequest request = new()
		{
			BucketName = bucket
		};
		var response = await client.DeleteBucketAsync(request, cancelToken);
		if (response.HttpStatusCode >= System.Net.HttpStatusCode.BadRequest)
		{
			throw new IOException("Failed to delete bucket, status code " + response.HttpStatusCode);
		}
	}

	/// <inheritdoc />
	public async Task<IReadOnlyCollection<S3Bucket>> ListBucketsAsync(CancellationToken cancelToken = default)
	{
		if (isProduction)
		{
			throw new NotSupportedException("Buckets cannot be listed programatically in production");
		}

		ListBucketsResponse response = await client.ListBucketsAsync(cancelToken);
		if (response.HttpStatusCode >= System.Net.HttpStatusCode.BadRequest)
		{
			throw new IOException("Failed to delete bucket, status code " + response.HttpStatusCode);
		}
		return response.Buckets;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyCollection<S3Object>> ListBucketContentsAsync(string bucketName, string? prefix = null, string? continuationToken = null, int maxKeys = 1000, CancellationToken cancelToken = default)
	{
		ListObjectsV2Request request = new()
		{
			BucketName = bucketName,
			Prefix = prefix,
			ContinuationToken = continuationToken,
			MaxKeys = maxKeys
		};

		ListObjectsV2Response response = await client.ListObjectsV2Async(request, cancelToken);
		if (response.HttpStatusCode >= System.Net.HttpStatusCode.BadRequest)
		{
			throw new IOException("Failed to delete bucket, status code " + response.HttpStatusCode);
		}
		return response.S3Objects;
	}
}

/// <summary>
/// S3 config
/// </summary>
/// <param name="AccessKey">Access key</param>
/// <param name="SecretKey">Secret key</param>
/// <param name="Url">Region</param>
/// <param name="DisableSigning">Whether to disable signing, needed for cloudflare r2 for example</param>
public record S3Config(string AccessKey, string SecretKey, string Url, bool DisableSigning = false);
