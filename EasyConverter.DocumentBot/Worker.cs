using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EasyConverter.LibreOffice;
using EasyConverter.Shared;
using EasyConverter.Shared.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EasyConverter.DocumentBot
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly MessageQueueService _messageQueue;
        private readonly IStorageProvider _provider;

        private readonly string _workFolder = @$"F:\converter\work";
        private readonly string _outputFolder = $@"F:\converter\work\out";

        public Worker(
            ILogger<Worker> logger,
            MessageQueueService messageQueue,
            IStorageProvider provider)
        {
            _logger = logger;
            _messageQueue = messageQueue;
            _provider = provider;
            _logger.LogInformation("Document Bot, Up and running...");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory() { HostName = "localhost", DispatchConsumersAsync = true };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                var queueName = Shared.Constants.QueueNames.ConvertDocument;
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
                    var job = Serializer.Deserialize<ConvertDocumentJob>(body);

                    if (await ProcessJob(job))
                    {
                        channel.BasicAck(deliveryTag: args.DeliveryTag, multiple: false);
                    }
                }
            }
        }

        private async Task<bool> ProcessJob(ConvertDocumentJob job)
        {
            try
            {
                _logger.LogInformation("Recieved '{JobName}' job.", job.Name);

                if (string.IsNullOrWhiteSpace(job.OriginalExtension))
                {
                    _logger.LogError("Job '{JobName}' failed because it didn't contain the original extension.", job.Name);
                }

                var fileName = job.FileId + "." + job.OriginalExtension;
                var fullPath = Path.Combine(_workFolder, fileName);

                using (var file = File.OpenWrite(fullPath))
                {
                    var storageFile = await _provider.GetObject(Shared.Constants.Buckets.Original, job.FileId);
                    using (var stream = storageFile.Data)
                    {
                        await stream.CopyToAsync(file);
                    }
                }

                var result = Converter.Convert(fullPath, job.DesiredExtension, _workFolder);
                if (result.Successful)
                {
                    var resultPath = await CopyFile(result.OutputFile, _outputFolder);
                    var info = new FileInfo(resultPath);

                    var contentType = KitchenSink.GetContentTypeFromExtension(info.Extension);

                    using (var file = File.OpenRead(resultPath))
                    {
                        await _provider.UploadObject(file, Shared.Constants.Buckets.Result, job.FileId, contentType);
                    }

                    var notifyJob = new NotifyUserJob
                    {
                        IsSuccessful = true,
                        FileId = job.FileId
                    };

                    _messageQueue.QueueJob(notifyJob);

                    _logger.LogInformation("Job '{JobName}' was processed successfuly. Took {milliseconds} ms.", job.Name, result.Time.TotalMilliseconds);
                    return true;
                }
                else if (result.TimedOut)
                {
                    _logger.LogError("Job '{JobName}' timed out after {milliseconds} ms.", job.Name, result.Time.TotalMilliseconds);

                    // TODO: Let user know when task fails
                }
                else
                {
                    _logger.LogError("Job '{JobName}' failed. Reason: {Reason} Took {milliseconds} ms.", job.Name, result.Output, result.Time.TotalMilliseconds);

                    // TODO: Let user know when task fails
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job 'JobName' failed because: {Message}.", job.Name, ex.Message);
            }

            return false;
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
