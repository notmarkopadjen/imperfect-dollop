using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Paden.ImperfectDollop.FallbackStrategies
{
    public class OneRetryThenRPCFallbackStrategy : IFallbackStrategy
    {
        private readonly ILogger logger;

        public OneRetryThenRPCFallbackStrategy(ILogger logger = null)
        {
            this.logger = logger;
        }
        public IEnumerable<T> GetAll<T, TId>(Repository<T, TId> repository, Exception exception) where T : Entity<TId>, new()
        {
            try
            {
                var result = repository.GetAll();
                repository.SourceConnectionAliveSince = DateTime.UtcNow;
                return result;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Exception on OneRetryThenRPCFallbackStrategy.GetAll - retry didn't work");
            }
            return repository.FallbackFunction();
        }
    }
}
