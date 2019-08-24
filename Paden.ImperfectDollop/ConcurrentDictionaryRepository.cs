using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Paden.ImperfectDollop
{
    public abstract class ConcurrentDictionaryRepository<T, TId> : Repository<T, TId> where T : Entity<TId>, new()
    {
        ConcurrentDictionary<TId, T> store;

        public override bool IsThreadSafe => true;
        public override ulong ItemCount => (ulong)store.Count;

        public ConcurrentDictionaryRepository(IBroker broker = null) : base(broker)
        {
            store = new ConcurrentDictionary<TId, T>(
                         base.GetAll().Select(l => new KeyValuePair<TId, T>(l.Id, l)));
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
                    try
                    {
                        store = new ConcurrentDictionary<TId, T>(
                         base.GetAll().Select(l => new KeyValuePair<TId, T>(l.Id, l)));
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

        public override StatusCode Create(T entity)
        {
            return BaseCRUD(
                () => base.Create(entity),
                () => CreateReceived(entity)
            );
        }

        public override StatusCode Update(T entity)
        {
            return BaseCRUD(
                () => base.Update(entity),
                () => UpdateReceived(entity)
            );
        }

        public override StatusCode Delete(TId id)
        {
            return BaseCRUD(
                () => base.Delete(id),
                () => DeleteReceived(id)
            );
        }

        private StatusCode BaseCRUD(Func<StatusCode> baseFunction, Action successAction)
        {
            try
            {
                var result = baseFunction();
                if (result == StatusCode.Success)
                {
                    successAction();
                }
                return result;
            }
            catch (Exception)
            {
                return StatusCode.UnkownError;
            }
        }

        public override void CreateReceived(T entity)
        {
            store.TryAdd(entity.Id, entity);
        }

        public override void UpdateReceived(T entity)
        {
            store.AddOrUpdate(entity.Id, id => entity, (id, oldValue) => oldValue.Version < entity.Version ? entity : oldValue);
        }

        public override void DeleteReceived(TId id)
        {
            store.TryRemove(id, out var _);
        }
    }
}
