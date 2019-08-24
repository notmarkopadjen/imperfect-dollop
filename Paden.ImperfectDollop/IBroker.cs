using System;

namespace Paden.ImperfectDollop
{
    public interface IBroker : IDisposable
    {
        bool IsMultiThreaded { get; }
        bool SupportsRemoteProcedureCall { get; }
        void ListenFor<T, TId>(Repository<T, TId> repository) where T : Entity<TId>, new();
        void StartRPC<T, TId>(Repository<T, TId> repository) where T : Entity<TId>, new();
    }
}