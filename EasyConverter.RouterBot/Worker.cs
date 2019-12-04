using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EasyConverter.Shared;
using EasyConverter.Shared.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EasyConverter.RouterBot
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly MessageQueueService _messageQueue;
        private readonly IStorageProvider _storageProvider;

        public Worker(
            ILogger<Worker> logger,
            MessageQueueService messageQueue,
            IStorageProvider storageProvider)
        {
            _logger = logger;
            _messageQueue = messageQueue;
            _storageProvider = storageProvider;
            _logger.LogInformation("Router Bot, Up and running...");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory() { HostName = "localhost", DispatchConsumersAsync = true };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                var queueName = Shared.Constants.QueueNames.StartConversionJob;
                channel.QueueDeclare(queue: queueName,
                                     durable: true,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);

                channel.BasicQos(0, 1, false);

                _logger.LogInformation("Waiting for messages...");

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += HandleMessage;

                channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, stoppingToken);
                }

                async Task HandleMessage(object sender, BasicDeliverEventArgs args)
                {
                    var body = args.Body;
                    var job = Serializer.Deserialize<StartConversionJob>(body);

                    if (await ProcessJob(job))
                    {
                        channel.BasicAck(deliveryTag: args.DeliveryTag, multiple: false);
                    }
                }
            }
        }

        private async Task<bool> ProcessJob(StartConversionJob job)
        {
            try
            {
                _logger.LogInformation("Recieved '{JobName}' job.", job.Name);

                var storageObject = await _storageProvider.GetObjectMetadata(
                    Shared.Constants.Buckets.Original,
                    job.FileId);

                if (storageObject.Metadata.ContainsKey(Shared.Constants.Metadata.FileType) &&
                    storageObject.Metadata.ContainsKey(Shared.Constants.Metadata.ConvertTo))
                {
                    var fileType = storageObject.Metadata[Shared.Constants.Metadata.FileType];
                    var convertTo = storageObject.Metadata[Shared.Constants.Metadata.ConvertTo];

                    var newJob = new ConvertDocumentJob
                    {
                        DesiredExtension = convertTo,
                        FileId = job.FileId,
                        OriginalExtension = GetExtension(fileType),
                        Name = $"Convert document from {fileType} to {convertTo}."
                    };

                    _messageQueue.QueueJob(newJob);

                    _logger.LogInformation("Job '{JobName}' was processed successfuly.", job.Name);
                    return true;
                }
                else
                {
                    _logger.LogError("The file ('{FileId}') accompanying Job '{JobName}' doesn't have enough metadata.", job.FileId, job.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job 'JobName' failed because: {Message}.", job.Name, ex.Message);
            }

            return false;
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
    }
}
