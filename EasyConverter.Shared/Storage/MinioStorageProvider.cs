using Minio.DataModel;
using Minio.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EasyConverter.Shared.Storage
{
    public class StorageObject : IStorageObject
    {
        public string Name { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
        public string ContentType { get; set; }
        public DateTime LastModified { get; set; }
        public long Size { get; set; }
        public Stream Data { get; set; }
    }

    public class MinioStorageProvider : IStorageProvider
    {
        private const string MetadataPrefix = "x-amz-meta-";
        private readonly Minio.MinioClient _client;

        public MinioStorageProvider(string endpoint, string accessKey, string secret)
        {
            _client = new Minio.MinioClient(endpoint, accessKey, secret);
        }

        public async Task UploadObject(
            Stream data,
            string bucketName,
            string objectName,
            string contentType = "application/octet-stream",
            Dictionary<string, string> metadata = null,
            CancellationToken token = default)
        {
            await EnsureBucketExists(bucketName, token);

            if (await ObjectExists(bucketName, objectName, token))
            {
                await RemoveObject(bucketName, objectName, token);
            }

            if (metadata != null && metadata.Count > 0)
            {
                foreach (var key in metadata.Keys.ToArray())
                {
                    if (key.StartsWith(MetadataPrefix, StringComparison.OrdinalIgnoreCase) == false)
                    {
                        metadata[$"{MetadataPrefix}{key}"] = metadata[key];
                        metadata.Remove(key);
                    }
                }
            }

            await _client.PutObjectAsync(
                bucketName,
                objectName,
                data,
                data.Length,
                contentType: contentType,
                metaData: metadata);
        }

        private static Dictionary<string, string> StripPrefixes(Dictionary<string, string> metadata)
        {
            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in metadata.Keys)
            {
                if (key.StartsWith(MetadataPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    dictionary[key.Substring(MetadataPrefix.Length)] = metadata[key];
                }
            }

            return dictionary;
        }

        public async Task<IStorageObject> GetObjectMetadata(string bucketName, string objectName, CancellationToken token = default)
        {
            var stats = await StatObject(bucketName, objectName, token);
            if (stats is null) return null;

            return new StorageObject
            {
                ContentType = stats.ContentType,
                LastModified = stats.LastModified,
                Metadata = StripPrefixes(stats.MetaData),
                Name = stats.ObjectName,
                Size = stats.Size
            };
        }

        public async Task<bool> ObjectExists(string bucketName, string objectName, CancellationToken token = default)
        {
            return await StatObject(bucketName, objectName, token) != null;
        }

        private async Task<ObjectStat> StatObject(string bucketName, string objectName, CancellationToken token = default)
        {
            try
            {
                return await _client.StatObjectAsync(bucketName, objectName, cancellationToken: token);
            }
            catch (MinioException)
            {
                return null;
            }
        }

        public async Task<IStorageObject> GetObject(string bucketName, string objectName, CancellationToken token = default)
        {
            ObjectStat stats = await StatObject(bucketName, objectName, token);
            if (stats is null)
                return null;

            var stream = new MemoryStream();

            await _client.GetObjectAsync(bucketName, objectName, input =>
            {
                input.CopyTo(stream);
                stream.Seek(0, SeekOrigin.Begin);
                input.Dispose();
            }, cancellationToken: token);

            return new StorageObject
            {
                ContentType = stats.ContentType,
                Data = stream,
                Metadata = StripPrefixes(stats.MetaData),
                Name = stats.ObjectName,
                Size = stats.Size,
                LastModified = stats.LastModified
            };
        }

        public async Task EnsureBucketExists(string bucketName, CancellationToken token = default)
        {
            if (!await _client.BucketExistsAsync(bucketName, token))
            {
                await _client.MakeBucketAsync(bucketName, cancellationToken: token);
            }
        }

        public async Task CopyObject(
            string bucketName,
            string objectName,
            string destBucketName,
            string destObjectName = null,
            Dictionary<string, string> metadata = null,
            CancellationToken token = default)
        {
            await EnsureBucketExists(destBucketName, token);

            await _client.CopyObjectAsync(bucketName, objectName, destBucketName, destObjectName, metadata: metadata, cancellationToken: token);
        }

        public async Task RemoveObject(string bucketName, string objectName, CancellationToken token = default)
        {
            await _client.RemoveObjectAsync(bucketName, objectName, token);
        }

        public async Task<string> GetPresignedDownloadLink(string bucketName, string objectName, TimeSpan expirationTime)
        {
            return await _client.PresignedGetObjectAsync(bucketName, objectName, (int)expirationTime.TotalSeconds);
        }
    }

    public static class MinioStorageProviderFactory
    {
        public static MinioStorageProvider Create()
        {
            var accessKey = Environment.GetEnvironmentVariable("MINIO_ACCESS_KEY", EnvironmentVariableTarget.Machine);
            var secret = Environment.GetEnvironmentVariable("MINIO_SECRET", EnvironmentVariableTarget.Machine);
            var endpoint = Environment.GetEnvironmentVariable("MINIO_ENDPOINT", EnvironmentVariableTarget.Machine);

            return new MinioStorageProvider(endpoint, accessKey, secret);
        }
    }
}
