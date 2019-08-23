using System;
using System.Collections.Generic;

namespace Paden.ImperfectDollop.FallbackStrategies
{
    public interface IFallbackStrategy
    {
        IEnumerable<T> GetAll<T, TId>(Repository<T, TId> repository, Exception exception) where T : Entity<TId>, new();
    }
}
