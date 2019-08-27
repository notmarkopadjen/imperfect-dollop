using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Paden.ImperfectDollop
{
    public abstract class ConcurrentDictionaryRepository<T, TId> : Repository<T, TId> where T : Entity<TId>, new()
    {
        Lazy<ConcurrentDictionary<TId, T>> store;

        public override bool IsThreadSafe => true;
        public override ulong ItemCount => (ulong)store.Value.Count;

        public ConcurrentDictionaryRepository(ILogger logger = null, IBroker broker = null) : base(logger, broker)
        {
            store = new Lazy<ConcurrentDictionary<TId, T>>(StoreFactory);
        }

        ConcurrentDictionary<TId, T> StoreFactory()
        {
            return new ConcurrentDictionary<TId, T>(base.GetAll().Select(l => new KeyValuePair<TId, T>(l.Id, l)));
        }

        bool isRefreshing;
        private void RefreshIfRequired()
        {
            if (isRefreshing) return;
            if (ExpiryInterval.HasValue && !LastSourceRead.HasValue || DateTime.UtcNow - LastSourceRead.Value >= ExpiryInterval)
            {
                Task.Run(() =>
                {
                    isRefreshing = true;
                    logger?.LogTrace("Refreshing data source");
                    try
                    {
                        store = new Lazy<ConcurrentDictionary<TId, T>>(StoreFactory);
                        var s = store.Value;
                        logger?.LogTrace("Updated store {0}", s);
                    }
                    finally
                    {
                        isRefreshing = false;
                    }
                });
            }
        }

        public override IEnumerable<T> GetAll()
        {
            var s = store.Value;
            RefreshIfRequired();
            return s.Values;
        }

        public override T Get(TId id)
        {
            var s = store.Value;
            RefreshIfRequired();
            return s.TryGetValue(id, out var entity) ? entity : default;
        }
        protected override T GetFromSource(TId id)
        {
            throw new NotImplementedException();
        }

        public override void CreateReceived(T entity)
        {
            store.Value.TryAdd(entity.Id, entity);
        }

        public override void UpdateReceived(T entity)
        {
            store.Value.AddOrUpdate(entity.Id, id => entity, (id, oldValue) => oldValue.Version < entity.Version ? entity : oldValue);
        }

        public override void DeleteReceived(TId id)
        {
            store.Value.TryRemove(id, out var _);
        }
    }
}
