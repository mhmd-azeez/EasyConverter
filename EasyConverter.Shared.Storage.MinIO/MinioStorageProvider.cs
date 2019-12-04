using Minio.DataModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EasyConverter.Shared.Storage.MinIO
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

            await _client.PutObjectAsync(
                bucketName,
                objectName,
                data,
                data.Length,
                contentType: contentType,
                metaData: metadata);
        }

        public async Task<IStorageObject> GetObject(string bucketName, string objectName, CancellationToken token = default)
        {
            var stream = new MemoryStream();

            await _client.GetObjectAsync(bucketName, objectName, input => input.CopyTo(stream), cancellationToken: token);
            Minio.DataModel.ObjectStat stats = await _client.StatObjectAsync(bucketName, objectName, cancellationToken: token);

            var dictionary = new Dictionary<string, string>();

            foreach (var pair in stats.MetaData)
            {
                dictionary.Add(pair.Key, pair.Value);
            }

            return new StorageObject
            {
                ContentType = stats.ContentType,
                Data = stream,
                Metadata = dictionary,
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
    }
}
