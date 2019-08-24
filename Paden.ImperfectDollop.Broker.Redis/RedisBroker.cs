using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;

namespace Paden.ImperfectDollop.Broker.Redis
{
    public class RedisBroker : IBroker
    {
        static readonly Lazy<IConfigurationRoot> config;
        static readonly Lazy<string> redisUri;

        static RedisBroker()
        {
            config = new Lazy<IConfigurationRoot>(() => new ConfigurationBuilder().AddJsonFile("appsettings.json", false, false).AddEnvironmentVariables().Build());
            redisUri = new Lazy<string>(() => config.Value["Redis:Uri"]);
        }
        string ClientId { get; } = $"{Environment.MachineName}.{Guid.NewGuid():N}";

        public bool IsMultiThreaded => false;


        readonly ConnectionMultiplexer connection;
        readonly IDatabase database;

        public RedisBroker()
        {
            connection = ConnectionMultiplexer.Connect(redisUri.Value);
            database = connection.GetDatabase();
        }

        public void Dispose()
        {
            connection?.Dispose();
        }

        public void StartFor<T, TId>(Repository<T, TId> repository) where T : Entity<TId>, new()
        {
            var xName = "Paden.ImperfectDollop." + typeof(T);
            var qName = $"{xName}.{ClientId}";

            var sub = connection.GetSubscriber();

            sub.Subscribe(xName, (channel, message) =>
            {
                var action = JsonConvert.DeserializeObject<EntityEventArgs<T>>(message);
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
                sub.Publish(xName, JsonConvert.SerializeObject(a));
            };

            repository.EntityCreated += repositoryAction;
            repository.EntityUpdated += repositoryAction;
            repository.EntityDeleted += repositoryAction;
        }
    }
}
