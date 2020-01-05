using EasyConverter.LibreOffice;
using EasyConverter.Shared;
using EasyConverter.Shared.Storage;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

namespace EasyConverter.WebUI.Jobs
{
    public class ConvertDocumentJob
    {
        private readonly ILogger<ConvertDocumentJob> _logger;
        private readonly IStorageProvider _provider;
        private readonly IBackgroundJobClient _backgroundJobs;
        private readonly string _workPath;
        private readonly string _outputPath;

        public ConvertDocumentJob(
            ILogger<ConvertDocumentJob> logger,
            IStorageProvider provider,
            IBackgroundJobClient backgroundJobs,
            IConfiguration configuration)
        {
            _logger = logger;
            _provider = provider;
            _backgroundJobs = backgroundJobs;
            _workPath = configuration["Jobs:ConvertDocument:WorkPath"];
            _outputPath = configuration["Jobs:ConvertDocument:OutputPath"];
        }

        [DisplayName("Convert document: {0}")]
        public async Task Run(string fileId)
        {
            EnsureFolderExists(_workPath);
            EnsureFolderExists(_outputPath);

            var shallowStorageObject = await _provider.GetObjectMetadata(
                    Shared.Constants.Buckets.Original,
                    fileId);

            if (shallowStorageObject.Metadata.ContainsKey(Shared.Constants.Metadata.FileType) &&
                shallowStorageObject.Metadata.ContainsKey(Shared.Constants.Metadata.ConvertTo))
            {
                var extension = shallowStorageObject.Metadata[Shared.Constants.Metadata.FileType];
                var convertTo = shallowStorageObject.Metadata[Shared.Constants.Metadata.ConvertTo];

                var fileName = fileId + "." + extension;
                var fullPath = Path.Combine(_workPath, fileName);

                using (var file = File.OpenWrite(fullPath))
                {
                    var storageFile = await _provider.GetObject(Shared.Constants.Buckets.Original, fileId);
                    using (var stream = storageFile.Data)
                    {
                        await stream.CopyToAsync(file);
                    }
                }

                var result = Converter.Convert(fullPath, convertTo, _workPath);
                if (result.Successful)
                {
                    var resultPath = await CopyFile(result.OutputFile, _outputPath);
                    var info = new FileInfo(resultPath);

                    var contentType = KitchenSink.GetContentTypeFromExtension(info.Extension);

                    using (var file = File.OpenRead(resultPath))
                    {
                        await _provider.UploadObject(
                            file,
                            Shared.Constants.Buckets.Result,
                            fileId,
                            contentType,
                            shallowStorageObject.Metadata);
                    }

                    // TODO: Notify user document has been completed and is ready for downloading
                    _backgroundJobs.Enqueue<NotifyConversionSuccessfulJob>(job => job.Run(fileId));
                }
                else if (result.TimedOut)
                {
                    // TODO: Let user know when task fails
                }
                else
                {
                    // TODO: Let user know when task fails
                }
            }
        }

        private async Task<string> CopyFile(string filePath, string destFolder, string destFileName = null)
        {
            var info = new FileInfo(filePath);
            var dirInfo = Directory.CreateDirectory(destFolder);
            var destinationPath = Path.Combine(dirInfo.FullName, destFileName ?? info.Name);

            using (var source = File.OpenRead(filePath))
            using (var dest = File.OpenWrite(destinationPath))
            {
                dest.SetLength(0);
                await source.CopyToAsync(dest);
                return destinationPath;
            }
        }

        private void EnsureFolderExists(string folder)
        {
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }

        private string GetExtension(string contentType)
        {
            return contentType switch
            {
                "application/pdf" => "pdf",
                "application/msword" => "doc",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => "docx",
                "application/vnd.ms-excel" => "xls",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => "xlsx",
                "application/vnd.ms-powerpoint" => "ppt",
                "application/vnd.openxmlformats-officedocument.presentationml.presentation" => "pptx",
                "application/vnd.oasis.opendocument.text" => "odt",
                "application/vnd.oasis.opendocument.spreadsheet" => "ods",
                "application/vnd.oasis.opendocument.presentation" => "odp",
                _ => throw new IndexOutOfRangeException(),
            };
        }
    }
}
