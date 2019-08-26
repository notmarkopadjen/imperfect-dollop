using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Paden.ImperfectDollop.Broker.RabbitMQ
{
    public class RPCClient<T> : IDisposable
    {
        private const int rpcTimeoutMiliseconds = 10_000;

        private readonly IModel channel;
        private readonly string replyQueueName;
        private readonly EventingBasicConsumer consumer;
        private readonly BlockingCollection<string> respQueue = new BlockingCollection<string>();
        private readonly IBasicProperties props;
        private readonly ILogger logger;

        public static string RoutingKey { get; } = "Paden.ImperfectDollop.RPC." + typeof(T);

        public RPCClient(IConnection connection, ILogger logger)
        {
            this.logger = logger;
            channel = connection.CreateModel();
            replyQueueName = channel.QueueDeclare().QueueName;
            logger?.LogTrace("Creating reply queue: {0}", replyQueueName);
            consumer = new EventingBasicConsumer(channel);

            props = channel.CreateBasicProperties();
            var correlationId = Guid.NewGuid().ToString();
            logger?.LogTrace("Setting correlation Id: {0}", correlationId);
            props.CorrelationId = correlationId;
            props.ReplyTo = replyQueueName;

            consumer.Received += (model, ea) =>
            {
                var message = Encoding.UTF8.GetString(ea.Body);
                logger?.LogTrace("Queue {0} got message with CorrelationId '{1}':\r\n{2}", ea.RoutingKey, ea.BasicProperties.CorrelationId, message);
                if (ea.BasicProperties.CorrelationId == correlationId)
                {
                    respQueue.Add(message);
                }
            };
        }

        public IEnumerable<T> GetData()
        {
            logger?.LogTrace("Pushing to: {0}", RoutingKey);
            channel.BasicPublish(exchange: string.Empty, routingKey: RoutingKey, basicProperties: props);
            logger?.LogTrace("Reading from: {0}", replyQueueName);
            channel.BasicConsume(consumer: consumer, queue: replyQueueName, autoAck: true);
            if (respQueue.TryTake(out var item, rpcTimeoutMiliseconds))
            {
                return JsonConvert.DeserializeObject<IEnumerable<T>>(item);
            }
            throw new TimeoutException();
        }

        public void Dispose()
        {
            channel?.Close();
        }
    }
}
