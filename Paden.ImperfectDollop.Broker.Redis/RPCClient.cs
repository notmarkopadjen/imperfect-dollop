using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Paden.ImperfectDollop.Broker.Redis
{
    public class RPCClient<T>
    {
        private static TimeSpan loopInterval = TimeSpan.FromMilliseconds(100);
        private static TimeSpan replyTimeout = TimeSpan.FromSeconds(20);

        public static string RoutingKey { get; } = "Paden:ImperfectDollop:RPC:" + typeof(T);

        public static IEnumerable<T> GetData(IDatabase database)
        {
            var replyChannel = $"Paden:ImperfectDollop:RPC:Replies:{Guid.NewGuid():N}";
            database.ListRightPush(RoutingKey, replyChannel);

            string reply = default;
            var listenStartTime = DateTime.UtcNow;
            while(DateTime.UtcNow - listenStartTime < replyTimeout)
            {
                reply = database.ListRightPop(replyChannel);
                if (!string.IsNullOrEmpty(reply))
                {
                    break;
                }
                Thread.Sleep(loopInterval);
            }
            if (string.IsNullOrEmpty(reply))
            {
                throw new TimeoutException();
            }
            return JsonConvert.DeserializeObject<IEnumerable<T>>(reply);
        }
    }
}
