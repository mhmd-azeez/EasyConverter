using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EasyConverter.Shared.Storage
{
    public interface IStorageProvider
    {
        Task CopyObject(string bucketName, string objectName, string destBucketName, string destObjectName = null, Dictionary<string, string> metadata = null, CancellationToken token = default);
        Task EnsureBucketExists(string bucketName, CancellationToken token = default);
        Task<IStorageObject> GetObject(string bucketName, string objectName, CancellationToken token = default);
        Task<IStorageObject> GetObjectMetadata(string bucketName, string objectName, CancellationToken token = default);
        Task<bool> ObjectExists(string bucketName, string objectName, CancellationToken token = default);
        Task RemoveObject(string bucketName, string objectName, CancellationToken token = default);
        Task UploadObject(Stream data, string bucketName, string objectName, string contentType = "application/octet-stream", Dictionary<string, string> metadata = null, CancellationToken token = default);
        Task<string> GetPresignedDownloadLink(string bucketName, string objectName, TimeSpan expirationTime);
    }
}
