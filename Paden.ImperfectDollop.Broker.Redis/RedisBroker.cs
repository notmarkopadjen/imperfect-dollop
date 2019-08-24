using Microsoft.Extensions.Configuration;
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

        public bool IsMultiThreaded => false;

        public void Dispose()
        {

        }

        public void StartFor<T, TId>(Repository<T, TId> repository) where T : Entity<TId>, new()
        {

        }
    }
}
