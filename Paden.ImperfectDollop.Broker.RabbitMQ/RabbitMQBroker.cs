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
            config = new Lazy<IConfigurationRoot>(() => new ConfigurationBuilder().AddJsonFile("appsettings.json", false, false).AddEnvironmentVariables().Build());
            rabbitMQUri = new Lazy<string>(() => config.Value["RabbitMQ:Uri"]);
        }

        string ClientId { get; } = $"{Environment.MachineName}.{Guid.NewGuid():N}";
        readonly IConnection connection;
        readonly ConnectionFactory connectionFactory;

        public bool IsMultiThreaded => true;
        public bool SupportsRemoteProcedureCall => true;

        public RabbitMQBroker()
        {
            connectionFactory = new ConnectionFactory() { Uri = new Uri(rabbitMQUri.Value) };
            connection = connectionFactory.CreateConnection(ClientId);
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

        public void ListenFor<T, TId>(Repository<T, TId> repository) where T : Entity<TId>, new()
        {
            if (!repository.IsThreadSafe)
            {
                throw new ApplicationException($"Repository {repository} is not thread safe.");
            }

            var channel = connection.CreateModel();
            var xName = "Paden.ImperfectDollop." + typeof(T);
            var qName = $"{xName}.{ClientId}";
            channel.ExchangeDeclare(exchange: xName, ExchangeType.Fanout);
            channel.QueueDeclare(queue: qName, durable: false, exclusive: true, autoDelete: true, arguments: null);
            channel.QueueBind(queue: qName, exchange: xName, routingKey: qName /* will be ignored */, arguments: null);

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var action = JsonConvert.DeserializeObject<EntityEventArgs<T>>(Encoding.UTF8.GetString(ea.Body));
                if (action.OriginatorId == ClientId) return;
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
            channel.BasicConsume(queue: qName, autoAck: true, consumer: consumer);

            Repository<T, TId>.EntityEventHandler repositoryAction = (object sender, EntityEventArgs<T> a) =>
            {
                a.OriginatorId = ClientId;
                channel.BasicPublish(exchange: xName, routingKey: qName, basicProperties: null, body: Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(a)));
            };

            repository.EntityCreated += repositoryAction;
            repository.EntityUpdated += repositoryAction;
            repository.EntityDeleted += repositoryAction;
        }

        public void StartRPC<T, TId>(Repository<T, TId> repository) where T : Entity<TId>, new()
        {
            repository.FallbackFunction = () =>
            {
                using (var rpcClient = new RPCClient<T>(connection))
                {
                    return rpcClient.GetData();
                }
            };

            StartRPCServer(repository);
        }

        void StartRPCServer<T, TId>(Repository<T, TId> repository) where T : Entity<TId>, new()
        {
            IModel rpcChannel = null;

            Repository<T, TId>.SourceConnectionStateChangedEventHandler connectionStateChangedAction = null;
            connectionStateChangedAction = (object sender, SourceConnectionStateChangedEventArgs a) =>
            {
                if (a.IsAlive)
                {
                    repository.SourceConnectionStateChanged -= connectionStateChangedAction;
                    StartRPCServer(repository);
                }
                else
                {
                    rpcChannel?.Close();
                }
            };
            repository.SourceConnectionStateChanged += connectionStateChangedAction;

            if (!repository.SourceConnectionIsAlive) return;

            rpcChannel = connection.CreateModel();
            rpcChannel.QueueDeclare(queue: RPCClient<T>.RoutingKey, durable: false, exclusive: false, autoDelete: false, arguments: null);
            rpcChannel.BasicQos(0, 1, false);
            var rpcConsumer = new EventingBasicConsumer(rpcChannel);
            rpcChannel.BasicConsume(queue: RPCClient<T>.RoutingKey, autoAck: false, consumer: rpcConsumer);

            rpcConsumer.Received += (model, ea) =>
            {
                if (!repository.SourceConnectionAliveSince.HasValue)
                {
                    rpcChannel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                    try
                    {
                        rpcChannel.Close();
                        rpcChannel = null;
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                    return;
                }

                string response = null;

                var props = ea.BasicProperties;
                var replyProps = rpcChannel.CreateBasicProperties();
                replyProps.CorrelationId = props.CorrelationId;

                try
                {
                    response = JsonConvert.SerializeObject(repository.GetAll());
                }
                catch (Exception)
                {
                    // ignored
                    response = string.Empty;
                }
                finally
                {
                    rpcChannel.BasicPublish(exchange: string.Empty, routingKey: props.ReplyTo, basicProperties: replyProps, body: Encoding.UTF8.GetBytes(response));
                    rpcChannel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
            };
        }
    }
}
