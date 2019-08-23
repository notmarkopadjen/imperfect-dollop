using System;

namespace Paden.ImperfectDollop
{
    public interface IBroker : IDisposable
    {
        void StartFor<T, TId>(Repository<T, TId> repository) where T : Entity<TId>, new();
    }
}