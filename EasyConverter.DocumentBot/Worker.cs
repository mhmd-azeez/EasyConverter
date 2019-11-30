using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EasyConverter.LibreOffice;
using EasyConverter.Shared;
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
        private readonly string _tusFolder = @"F:\converter\tus";
        private readonly string _workFolder = @$"F:\converter\work";
        private readonly string _outputFolder = $@"F:\converter\out";
        private readonly string _resultFolder = $@"F:\converter\result";

        public Worker(ILogger<Worker> logger, MessageQueueService messageQueue)
        {
            _logger = logger;
            _messageQueue = messageQueue;
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

                    await ProcessJob(job);

                    channel.BasicAck(deliveryTag: args.DeliveryTag, multiple: false);
                }
            }
        }

        private async Task ProcessJob(ConvertDocumentJob job)
        {
            try
            {
                _logger.LogInformation("Recieved '{JobName}' job.", job.Name);

                if (string.IsNullOrWhiteSpace(job.OriginalExtension))
                {
                    _logger.LogError("Job '{JobName}' failed because it didn't contain the original extension.", job.Name);
                }

                var fileName = job.FileId + "." + job.OriginalExtension;

                var fullPath = await CopyFile(Path.Combine(_tusFolder, job.FileId), _workFolder, fileName);

                var result = Converter.Convert(fullPath, job.DesiredExtension, _outputFolder);
                if (result.Successful)
                {
                    var resultPath = await CopyFile(result.OutputFile, _resultFolder);
                    var info = new FileInfo(resultPath);

                    var notifyJob = new NotifyUserJob
                    {
                        IsSuccessful = true,
                        FileName = info.Name,
                    };

                    _messageQueue.QueueJob(notifyJob);

                    _logger.LogInformation("Job '{JobName}' was processed successfuly. Took {milliseconds} ms.", job.Name, result.Time.TotalMilliseconds);
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
