using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System;

namespace EasyConverter.Shared
{
    public class MessageQueueService
    {
        private readonly ILogger<MessageQueueService> _logger;

        public MessageQueueService(ILogger<MessageQueueService> logger)
        {
            _logger = logger;
        }

        public bool QueueJob(IJob job)
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                var queueName = Enum.GetName(typeof(JobType), job.Type);
                channel.QueueDeclare(queue: queueName,
                                     durable: true,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);

                var body = Serializer.SerializeToBytes(job);

                var properties = channel.CreateBasicProperties();
                properties.Persistent = true;

                channel.BasicPublish(exchange: "",
                                     routingKey: queueName,
                                     basicProperties: properties,
                                     body: body);

                _logger.LogInformation("Queued Job: {JobName} of type {JobType}.", job.Name, queueName);
                return true;
            }
        }
    }
}
