using System;
using System.Collections.Generic;
using System.Linq;

namespace Paden.ImperfectDollop
{
    public abstract class DictionaryRepository<T, TId> : Repository<T, TId> where T : Entity<TId>, new()
    {
        Dictionary<TId, T> store;

        public override bool IsThreadSafe => false;
        public override ulong ItemCount => (ulong)store.Count;

        public DictionaryRepository(IBroker broker = null) : base(broker)
        {
            store = new Dictionary<TId, T>(
                base.GetAll().Select(l => new KeyValuePair<TId, T>(l.Id, l))
            );
        }

        private void RefreshIfRequired()
        {
            if (ExpiryInterval.HasValue && !LastSourceRead.HasValue || DateTime.UtcNow - LastSourceRead.Value >= ExpiryInterval)
            {
                store = new Dictionary<TId, T>(
                    base.GetAll().Select(l => new KeyValuePair<TId, T>(l.Id, l))
                );
            }
        }

        public override IEnumerable<T> GetAll()
        {
            RefreshIfRequired();
            return store.Values;
        }

        public override T Get(TId id)
        {
            RefreshIfRequired();
            return store.TryGetValue(id, out var entity) ? entity : default;
        }
        protected override T GetFromSource(TId id)
        {
            throw new NotImplementedException();
        }

        public override void CreateReceived(T entity)
        {
            store.TryAdd(entity.Id, entity);
        }

        public override void UpdateReceived(T entity)
        {
            if (store.TryGetValue(entity.Id, out var oldValue))
            {
                if (oldValue.Version < entity.Version)
                {
                    store[entity.Id] = entity;
                }
            }
            else
            {
                CreateReceived(entity);
            }
        }

        public override void DeleteReceived(TId id)
        {
            store.Remove(id, out var _);
        }
    }
}
