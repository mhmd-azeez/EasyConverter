using System;
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

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
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

                Console.WriteLine(" [*] Waiting for messages.");

                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += (model, ea) =>
                {
                    var body = ea.Body;
                    var job = Serializer.Deserialize<ConvertDocumentJob>(body);


                    ProcessJob(job);

                    channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                };

                channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);

                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Heartbeat.", DateTimeOffset.Now);
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }

        private void ProcessJob(ConvertDocumentJob job)
        {
            try
            {
                _logger.LogInformation("Recieved '{JobName}' job.", job.Name);

                var tusFolder = @"F:\converter\tus";
                var workFolder = @$"F:\converter\work";
                var outputFolder = $@"F:\converter\out";

                if (string.IsNullOrWhiteSpace(job.OriginalExtension))
                {
                    _logger.LogError("Job '{JobName}' failed because it didn't contain the original extension.", job.Name);
                }

                var fileName = Path.Combine(workFolder, job.FileId + "." + job.OriginalExtension);

                File.Copy(Path.Combine(tusFolder, job.FileId), fileName);

                var result = Converter.Convert(fileName, job.DesiredExtension, outputFolder);
                if (result.Successful)
                {
                    _logger.LogInformation("Job '{JobName}' was processed successfuly. Took {milliseconds} ms.", job.Name, result.Time.TotalMilliseconds);
                }
                else if (result.TimedOut)
                {
                    _logger.LogError("Job '{JobName}' timed out after {milliseconds} ms.", job.Name, result.Time.TotalMilliseconds);
                }
                else
                {
                    _logger.LogError("Job '{JobName}' failed. Reason: {Reason} Took {milliseconds} ms.", job.Name, result.Output, result.Time.TotalMilliseconds);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job 'JobName' failed because: {Message}.", job.Name, ex.Message);
            }
        }
    }
}
