using System;
using System.Collections.Generic;

namespace Paden.ImperfectDollop.FallbackStrategies
{
    public class OneRetryThenRPCFallbackStrategy : IFallbackStrategy
    {
        public IEnumerable<T> GetAll<T, TId>(Repository<T, TId> repository, Exception exception) where T : Entity<TId>, new()
        {
            try
            {
                var result = repository.GetAll();
                repository.SourceConnectionAliveSince = DateTime.UtcNow;
                return result;
            }
            catch (Exception)
            {
                // ignored
            }
            return repository.FallbackFunction();
        }
    }
}
