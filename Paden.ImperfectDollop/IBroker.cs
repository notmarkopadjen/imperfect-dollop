using System;

namespace Paden.ImperfectDollop
{
    public interface IBroker : IDisposable
    {
        bool IsMultiThreaded { get; }
        void StartFor<T, TId>(Repository<T, TId> repository) where T : Entity<TId>, new();
        event EventHandler BeforeDispose;
    }
}