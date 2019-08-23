using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Paden.ImperfectDollop.Broker.RabbitMQ
{
    public class RPCClient<T> : IDisposable
    {
        private readonly IConnection connection;
        private readonly IModel channel;
        private readonly string replyQueueName;
        private readonly EventingBasicConsumer consumer;
        private readonly BlockingCollection<string> respQueue = new BlockingCollection<string>();
        private readonly IBasicProperties props;

        public static string RoutingKey { get; } = "Paden.ImperfectDollop.RPC." + typeof(T);

        public RPCClient(IConnection connection)
        {
            channel = connection.CreateModel();
            replyQueueName = channel.QueueDeclare().QueueName;
            consumer = new EventingBasicConsumer(channel);

            props = channel.CreateBasicProperties();
            var correlationId = Guid.NewGuid().ToString();
            props.CorrelationId = correlationId;
            props.ReplyTo = replyQueueName;

            consumer.Received += (model, ea) =>
            {
                if (ea.BasicProperties.CorrelationId == correlationId)
                {
                    respQueue.Add(Encoding.UTF8.GetString(ea.Body));
                }
            };
        }

        public IEnumerable<T> GetData()
        {
            channel.BasicPublish(exchange: string.Empty, routingKey: RoutingKey, basicProperties: props);
            channel.BasicConsume(consumer: consumer, queue: replyQueueName, autoAck: true);
            return JsonConvert.DeserializeObject<IEnumerable<T>>(respQueue.Take());
        }

        public void Dispose()
        {
            connection.Close();
        }
    }
}
