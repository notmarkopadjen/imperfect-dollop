using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;

namespace Paden.ImperfectDollop.Broker.RabbitMQ
{
    public class RabbitMQBroker : IBroker
    {
        static readonly Lazy<IConfigurationRoot> config;
        static readonly Lazy<string> rabbitMQUri;

        static RabbitMQBroker()
        {
            config = new Lazy<IConfigurationRoot>(() => new ConfigurationBuilder().AddJsonFile("appsettings.json", false, false).Build());
            rabbitMQUri = new Lazy<string>(() => config.Value["RabbitMQ:Uri"]);
        }

        IConnection connection;

        public RabbitMQBroker()
        {
            var factory = new ConnectionFactory() { Uri = new Uri(rabbitMQUri.Value) };
            //var factory = new ConnectionFactory() { HostName = "rabbitmq.local:42671" };
            connection = factory.CreateConnection();
        }

        public void Dispose()
        {
            try
            {
                connection?.Dispose();
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public void StartFor<T, TId>(Repository<T, TId> repository) where T : Entity<TId>, new()
        {
            var channel = connection.CreateModel();

            var qName = "ImperfectDollop." + typeof(T);

            channel.QueueDeclare(queue: qName,
                                 durable: false,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body;
                var message = Encoding.UTF8.GetString(body);

                var action = JsonConvert.DeserializeObject<EntityEventArgs<T>>(message);

                Console.WriteLine("Received {0}", action);

                switch (action.EntityAction)
                {
                    case EntityAction.Create:
                        repository.CreateReceived(action.Entity);
                        break;
                    case EntityAction.Update:
                        repository.UpdateReceived(action.Entity);
                        break;
                    case EntityAction.Delete:
                        repository.DeleteReceived(action.Entity.Id);
                        break;
                }
            };
            channel.BasicConsume(queue: qName,
                                 autoAck: true,
                                 consumer: consumer);

            Repository<T, TId> .EntityEventHandler repositoryAction = (object sender, EntityEventArgs<T> a) =>
            {
                channel.BasicPublish(exchange: "",
                                 routingKey: qName,
                                 basicProperties: null,
                                 body: Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(a)));
            };

            repository.EntityCreated += repositoryAction;
            repository.EntityUpdated += repositoryAction;
            repository.EntityDeleted += repositoryAction;
        }
    }
}
