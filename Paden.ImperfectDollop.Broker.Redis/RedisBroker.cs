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

        public TimeSpan RPCLoopInterval { get; } = TimeSpan.FromMilliseconds(100);

        public RedisBroker(string uri)
        {
            connection = ConnectionMultiplexer.Connect(uri);
            database = connection.GetDatabase();
        }

        public void Dispose()
        {
            connection?.Dispose();
        }

        public void ListenFor<T, TId>(Repository<T, TId> repository) where T : Entity<TId>, new()
        {
            var xName = "Paden:ImperfectDollop:" + typeof(T);
            var sub = connection.GetSubscriber();

            sub.Subscribe(xName, (channel, message) =>
            {
                var action = JsonConvert.DeserializeObject<EntityEventArgs<T>>(message);
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
                sub.Publish(xName, JsonConvert.SerializeObject(a));
            };

            repository.EntityCreated += repositoryAction;
            repository.EntityUpdated += repositoryAction;
            repository.EntityDeleted += repositoryAction;
        }

        public void StartRPC<T, TId>(Repository<T, TId> repository) where T : Entity<TId>, new()
        {
            repository.FallbackFunction = () => RPCClient<T>.GetData(database);
            StartRPCServer(repository);
        }
        
        void StartRPCServer<T, TId>(Repository<T, TId> repository) where T : Entity<TId>, new()
        {
            bool listen = default;

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
                    if (string.IsNullOrEmpty(replyChannel))
                    {
                        Thread.Sleep(RPCLoopInterval);
                    }
                    else
                    {
                        if (!repository.SourceConnectionAliveSince.HasValue)
                        {
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
                                database.ListRightPush(replyChannel, response);
                            }
                        }
                    }
                }
            });
        }
    }
}
