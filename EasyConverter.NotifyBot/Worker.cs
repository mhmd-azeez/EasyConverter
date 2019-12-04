using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyConverter.Shared;
using EasyConverter.Shared.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace EasyConverter.NotifyBot
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IStorageProvider _storageProvider;

        public Worker(ILogger<Worker> logger, IStorageProvider provider)
        {
            _logger = logger;
            _storageProvider = provider;
            _logger.LogInformation("Notify Bot, Up and running...");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory() { HostName = "localhost", DispatchConsumersAsync = true };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                var queueName = Shared.Constants.QueueNames.NotifyUser;
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
                    var job = Serializer.Deserialize<NotifyUserJob>(body);

                    _logger.LogInformation($"Recieved Job: {job.Name}");
                    var successful = await ProcessJob(job);

                    if (successful)
                        channel.BasicAck(deliveryTag: args.DeliveryTag, multiple: false);

                    _logger.LogInformation($"Job {(successful ? "completed successfuly" : "failed")}");
                }
            }
        }

        private async Task<bool> ProcessJob(NotifyUserJob job)
        {
            var link = await _storageProvider.GetPresignedDownloadLink(
                Shared.Constants.Buckets.Result, job.FileId, TimeSpan.FromHours(24));

            var key = Environment.GetEnvironmentVariable("SENDGRID_API_KEY", EnvironmentVariableTarget.User);
            var client = new SendGridClient(key);

            var message = new SendGridMessage();
            message.SetFrom(new EmailAddress("hello@easy-converter.com", "Easy Converter"));
            // TODO: Use actual email address
            message.AddTo("example@example.com");
            message.SetTemplateId("d-aad5b0e03ad94172804fbf77fb301d3b");
            message.SetTemplateData(new
            {
                DownloadLink = link
            });

            var response = await client.SendEmailAsync(message);

            if (response.StatusCode != System.Net.HttpStatusCode.Accepted)
            {
                var responseBody = await response.Body.ReadAsStringAsync();
                _logger.LogError("SendGrid: " + responseBody);
            }

            return response.StatusCode == System.Net.HttpStatusCode.Accepted;
        }
    }
}
