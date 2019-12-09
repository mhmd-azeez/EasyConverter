using EasyConverter.Shared.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Interfaces;
using tusdotnet.Models;

namespace EasyConverter.WebUI.Stores
{
    public class StorageProviderTusStore : ITusStore, ITusCreationStore, ITusReadableStore
    {
        public class StorageProviderTusFile : ITusFile
        {
            private readonly IStorageProvider _storageProvider;
            private readonly string _bucketName;
            private readonly string _objectName;

            public StorageProviderTusFile(IStorageProvider storageProvider, string bucketName, string objectName)
            {
                _storageProvider = storageProvider;
                _bucketName = bucketName;
                _objectName = objectName;
            }

            public async Task<Stream> GetContentAsync(CancellationToken cancellationToken)
            {
                var storageObject = await _storageProvider.GetObject(_bucketName, _objectName, cancellationToken);
                return storageObject?.Data;
            }

            public async Task<Dictionary<string, Metadata>> GetMetadataAsync(CancellationToken cancellationToken)
            {
                var storageObject = await _storageProvider.GetObjectMetadata(_bucketName, _objectName, cancellationToken);
                if (storageObject is null || storageObject.Metadata.ContainsKey(TusMetadata) == false)
                {
                    return null;
                }

                return Metadata.Parse(storageObject?.Metadata[TusMetadata]);
            }

            public string Id { get; }
        }

        private readonly IStorageProvider _provider;
        private readonly string _bucketName;

        public StorageProviderTusStore(
            IStorageProvider provider,
            string bucketName)
        {
            _provider = provider;
            _bucketName = bucketName;
        }

        private const string TusUploadLength = "tus-upload-length";
        private const string TusMetadata = "tus-meta-data";

        public async Task<string> CreateFileAsync(long uploadLength, string metadata, CancellationToken cancellationToken)
        {
            var id = CreateFileId();

            var parsedMetadata = Metadata.Parse(metadata);

            var dictionary = new Dictionary<string, string>
            {
                { TusUploadLength, uploadLength.ToString() },
                { TusMetadata, metadata },
            };

            foreach (var pair in parsedMetadata)
            {
                dictionary.Add(pair.Key, pair.Value.GetString(Encoding.UTF8));
            }

            var contentType = dictionary[Shared.Constants.Metadata.FileType];

            await _provider.UploadObject(
                data: Stream.Null,
                bucketName: _bucketName,
                objectName: id,
                metadata: dictionary,
                contentType: contentType,
                token: cancellationToken);

            return id;
        }

        private static string CreateFileId()
        {
            return Guid.NewGuid().ToString("n");
        }

        public async Task<string> GetUploadMetadataAsync(string fileId, CancellationToken cancellationToken)
        {
            var storageObject = await _provider.GetObjectMetadata(_bucketName, fileId, cancellationToken);
            if (storageObject is null) return null;

            return storageObject.Metadata[TusMetadata];
        }

        public async Task<long> AppendDataAsync(string fileId, Stream stream, CancellationToken cancellationToken)
        {
            var storageObject = await _provider.GetObject(_bucketName, fileId, cancellationToken);
            using var outputStream = new MemoryStream();

            if (storageObject.Size > 0)
                await storageObject.Data.CopyToAsync(outputStream);

            await stream.CopyToAsync(outputStream);

            outputStream.Seek(0, SeekOrigin.Begin);

            await _provider.UploadObject(outputStream, _bucketName, fileId, metadata: storageObject.Metadata, token: cancellationToken);
            return outputStream.Length;
        }

        public async Task<bool> FileExistAsync(string fileId, CancellationToken cancellationToken)
        {
            var exists = await _provider.ObjectExists(_bucketName, fileId, cancellationToken);
            return exists;
        }

        public async Task<long?> GetUploadLengthAsync(string fileId, CancellationToken cancellationToken)
        {
            var storageObject = await _provider.GetObjectMetadata(_bucketName, fileId, cancellationToken);
            if (storageObject is null) return null;

            long.TryParse(storageObject.Metadata[TusUploadLength], out var length);
            return length;
        }

        public async Task<long> GetUploadOffsetAsync(string fileId, CancellationToken cancellationToken)
        {
            var storageObject = await _provider.GetObjectMetadata(_bucketName, fileId, cancellationToken);
            if (storageObject is null) throw new KeyNotFoundException();

            return storageObject.Size;
        }

        public Task<ITusFile> GetFileAsync(string fileId, CancellationToken cancellationToken)
        {
            ITusFile file = new StorageProviderTusFile(_provider, _bucketName, fileId);
            return Task.FromResult(file);
        }
    }
}
