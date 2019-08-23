using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Paden.ImperfectDollop
{
    public abstract class ConcurrentDictionaryRepository<T, TId> : Repository<T, TId> where T : Entity<TId>, new()
    {
        readonly ConcurrentDictionary<TId, T> store;

        public override bool IsThreadSafe => true;
        public override ulong ItemCount => (ulong)store.Count;

        public ConcurrentDictionaryRepository()
        {
            store = new ConcurrentDictionary<TId, T>(
                    GetAllFromSource().Select(l => new KeyValuePair<TId, T>(l.Id, l))
                );
        }

        public override IEnumerable<T> GetAll()
        {
            return store.Values;
        }

        public override T Get(TId id)
        {
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
            store[entity.Id] = entity;
        }

        public override void DeleteReceived(TId id)
        {
            store.TryRemove(id, out var _);
        }
    }
}
