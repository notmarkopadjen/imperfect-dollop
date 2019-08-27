using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;

namespace Paden.ImperfectDollop.Broker.RabbitMQ
{
    public class RabbitMQBroker : IBroker
    {
        string ClientId { get; } = $"{Environment.MachineName}.{Guid.NewGuid():N}";
        readonly IConnection connection;
        readonly ConnectionFactory connectionFactory;
        private readonly ILogger logger;

        public bool IsMultiThreaded => true;
        public bool SupportsRemoteProcedureCall => true;

        public RabbitMQBroker(string uri, ILogger<RabbitMQBroker> logger = null)
        {
            this.logger = logger;
            logger?.LogTrace("Started initiating client: {0} with Uri: {1}", ClientId, uri);
            connectionFactory = new ConnectionFactory() { Uri = new Uri(uri) };
            connection = connectionFactory.CreateConnection(ClientId);
        }

        public void Dispose()
        {
            try
            {
                connection?.Dispose();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Exception while disposing");
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
            logger?.LogTrace("Creating exchange: {0}", xName);
            channel.QueueDeclare(queue: qName, durable: false, exclusive: true, autoDelete: true, arguments: null);
            logger?.LogTrace("Creating queue: {0}", qName);
            channel.QueueBind(queue: qName, exchange: xName, routingKey: qName /* will be ignored */, arguments: null);

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var message = Encoding.UTF8.GetString(ea.Body);
                var action = JsonConvert.DeserializeObject<EntityEventArgs<T>>(message);
                logger?.LogTrace("Queue {0} got message from {1}:\r\n{2}", qName, action.OriginatorId, message);
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
                var message = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(a));
                logger?.LogTrace("Pushing to {0}:\r\n{1}", qName, message);
                channel.BasicPublish(exchange: xName, routingKey: qName, basicProperties: null, body: message);
            };

            repository.EntityCreated += repositoryAction;
            repository.EntityUpdated += repositoryAction;
            repository.EntityDeleted += repositoryAction;
        }

        public void StartRPC<T, TId>(Repository<T, TId> repository) where T : Entity<TId>, new()
        {
            repository.FallbackFunction = () =>
            {
                logger?.LogTrace("Making RPC call for: {0}", repository);
                using (var rpcClient = new RPCClient<T>(connection, logger))
                {
                    logger?.LogTrace("Getting RPC data for: {0}", repository);
                    return rpcClient.GetData();
                }
            };

            logger?.LogTrace("Starting RPC for: {0}", repository);
            StartRPCServer(repository);
        }

        void StartRPCServer<T, TId>(Repository<T, TId> repository) where T : Entity<TId>, new()
        {
            IModel rpcChannel = null;

            Repository<T, TId>.SourceConnectionStateChangedEventHandler connectionStateChangedAction = null;
            connectionStateChangedAction = (object sender, SourceConnectionStateChangedEventArgs a) =>
            {
                logger?.LogTrace("Connection for repository {0} change to {1}", repository, a.IsAlive);
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
            logger?.LogTrace("Creating queue: {0}", RPCClient<T>.RoutingKey);
            rpcChannel.QueueDeclare(queue: RPCClient<T>.RoutingKey, durable: false, exclusive: false, autoDelete: false, arguments: null);
            rpcChannel.BasicQos(0, 1, false);
            var rpcConsumer = new EventingBasicConsumer(rpcChannel);
            rpcChannel.BasicConsume(queue: RPCClient<T>.RoutingKey, autoAck: false, consumer: rpcConsumer);

            rpcConsumer.Received += (model, ea) =>
            {
                if (!repository.SourceConnectionAliveSince.HasValue)
                {
                    logger?.LogTrace("Got message, but repository is down");
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

                logger?.LogTrace("Got RPC read request with ID {0}", replyProps.CorrelationId);

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
                    logger?.LogTrace("Pushing RPC reply to {0}:\r\n{1}", props.ReplyTo, response);
                    rpcChannel.BasicPublish(exchange: string.Empty, routingKey: props.ReplyTo, basicProperties: replyProps, body: Encoding.UTF8.GetBytes(response));
                    rpcChannel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
            };
        }
    }
}
