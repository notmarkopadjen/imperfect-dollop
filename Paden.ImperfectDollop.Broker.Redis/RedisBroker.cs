using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paden.ImperfectDollop.Broker.Redis
{
    public class RedisBroker : IBroker
    {
        string ClientId { get; } = $"{Environment.MachineName}.{Guid.NewGuid():N}";

        public bool IsMultiThreaded => false;
        public bool SupportsRemoteProcedureCall => true;

        readonly ConnectionMultiplexer connection;
        readonly IDatabase database;
        private readonly ILogger<RedisBroker> logger;

        public TimeSpan RPCLoopInterval { get; } = TimeSpan.FromMilliseconds(100);

        public RedisBroker(string uri, ILogger<RedisBroker> logger = null)
        {
            this.logger = logger;
            logger?.LogTrace("Started initiating client: {0} with Uri: {1}", ClientId, uri);
            connection = ConnectionMultiplexer.Connect(uri);
            database = connection.GetDatabase();
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
            var xName = "Paden:ImperfectDollop:" + typeof(T);
            var sub = connection.GetSubscriber();

            logger?.LogTrace("Subscribing to: {0}", xName);

            sub.Subscribe(xName, (channel, message) =>
            {
                var action = JsonConvert.DeserializeObject<EntityEventArgs<T>>(message);
                logger?.LogTrace("Topic {0} got message from {1}:\r\n{2}", xName, action.OriginatorId, message);
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
            });

            Repository<T, TId>.EntityEventHandler repositoryAction = (object sender, EntityEventArgs<T> a) =>
            {
                a.OriginatorId = ClientId;
                var message = JsonConvert.SerializeObject(a);
                logger?.LogTrace("Pushing to {0}:\r\n{1}", xName, message);
                sub.Publish(xName, message);
            };

            repository.EntityCreated += repositoryAction;
            repository.EntityUpdated += repositoryAction;
            repository.EntityDeleted += repositoryAction;
        }

        public void StartRPC<T, TId>(Repository<T, TId> repository) where T : Entity<TId>, new()
        {
            repository.FallbackFunction = () =>
            {
                logger?.LogTrace("Getting RPC data for: {0}", repository);
                return RPCClient<T>.GetData(database, logger);
            };
            logger?.LogTrace("Starting RPC for: {0}", repository);
            StartRPCServer(repository);
        }
        
        void StartRPCServer<T, TId>(Repository<T, TId> repository) where T : Entity<TId>, new()
        {
            bool listen = default;

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
                    listen = default;
                }
            };
            repository.SourceConnectionStateChanged += connectionStateChangedAction;

            if (!repository.SourceConnectionIsAlive) return;

            listen = true;

            Task.Run(() =>
            {
                string replyChannel = default;
                while (listen)
                {
                    replyChannel = database.ListRightPop(RPCClient<T>.RoutingKey);
                    logger?.LogTrace("Got RPC read request with reply ID {0}", replyChannel);
                    if (string.IsNullOrEmpty(replyChannel))
                    {
                        Thread.Sleep(RPCLoopInterval);
                    }
                    else
                    {
                        if (!repository.SourceConnectionAliveSince.HasValue)
                        {
                            logger?.LogTrace("Got message, but repository is down");
                            listen = false;
                            database.ListRightPush(RPCClient<T>.RoutingKey, replyChannel);
                        }
                        else
                        {
                            string response = default;
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
                                logger?.LogTrace("Pushing RPC reply to {0}:\r\n{1}", replyChannel, response);
                                database.ListRightPush(replyChannel, response);
                            }
                        }
                    }
                }
            });
        }
    }
}
