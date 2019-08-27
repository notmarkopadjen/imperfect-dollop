using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Paden.ImperfectDollop
{
    public abstract class DictionaryRepository<T, TId> : Repository<T, TId> where T : Entity<TId>, new()
    {
        Lazy<Dictionary<TId, T>> store;

        public override bool IsThreadSafe => false;
        public override ulong ItemCount => (ulong)store.Value.Count;

        public DictionaryRepository(ILogger logger = null, IBroker broker = null) : base(logger, broker)
        {
            store = new Lazy<Dictionary<TId, T>>(StoreFactory);
        }
        Dictionary<TId, T> StoreFactory()
        {
            return new Dictionary<TId, T>(base.GetAll().Select(l => new KeyValuePair<TId, T>(l.Id, l)));
        }

        private void RefreshIfRequired()
        {
            if (ExpiryInterval.HasValue && !LastSourceRead.HasValue || DateTime.UtcNow - LastSourceRead.Value >= ExpiryInterval)
            {
                logger?.LogTrace("Refreshing data source");
                store = new Lazy<Dictionary<TId, T>>(StoreFactory);
                var s = store.Value;
                logger?.LogTrace("Updated store {0}", s);
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
            if (store.Value.TryGetValue(entity.Id, out var oldValue))
            {
                if (oldValue.Version < entity.Version)
                {
                    store.Value[entity.Id] = entity;
                }
            }
            else
            {
                CreateReceived(entity);
            }
        }

        public override void DeleteReceived(TId id)
        {
            store.Value.Remove(id, out var _);
        }
    }
}
