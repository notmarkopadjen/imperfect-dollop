using System;
using System.Collections.Generic;

namespace Paden.ImperfectDollop
{
    public abstract class Repository<T, TId> where T : Entity<TId>, new()
    {
        public delegate void EntityEventHandler(object sender, EntityEventArgs<T> a);
        public event EntityEventHandler EntityCreated;
        public event EntityEventHandler EntityUpdated;
        public event EntityEventHandler EntityDeleted;

        public virtual IEnumerable<T> GetAll()
        {
            return GetAllFromSource();
        }
        protected abstract IEnumerable<T> GetAllFromSource();

        public virtual T Get(TId id)
        {
            return GetFromSource(id);
        }
        protected abstract T GetFromSource(TId id);

        public virtual StatusCode Create(T entity)
        {
            var result = CreateInSource(entity);
            EntityCreated?.Invoke(this, new EntityEventArgs<T>
            {
                EntityAction = EntityAction.Create,
                Entity = entity
            });
            return result;
        }
        protected abstract StatusCode CreateInSource(T entity);

        public virtual StatusCode Update(T entity)
        {
            entity.Version++;
            StatusCode result;
            try
            {
                result = UpdateInSource(entity);
            }
            catch (Exception)
            {
                entity.Version--;
                throw;
            }
            EntityUpdated?.Invoke(this, new EntityEventArgs<T>
            {
                EntityAction = EntityAction.Update,
                Entity = entity
            });
            return result;
        }
        protected abstract StatusCode UpdateInSource(T entity);

        public virtual StatusCode Delete(TId id)
        {
            var result = DeleteInSource(id);
            EntityDeleted?.Invoke(this, new EntityEventArgs<T>
            {
                EntityAction = EntityAction.Delete,
                Entity = new T { Id = id }
            });
            return result;
        }
        protected abstract StatusCode DeleteInSource(TId id);

        protected StatusCode ExecuteHandled(Action action, params Func<Exception, StatusCode?>[] handlers)
        {
            try
            {
                action();
                return StatusCode.Success;
            }
            catch (Exception ex)
            {
                foreach (var handler in handlers)
                {
                    var status = handler(ex);
                    if (status.HasValue)
                    {
                        return status.Value;
                    }
                }
                return StatusCode.UnkownError;
            }
        }

        public abstract void CreateReceived(T entity);
        public abstract void UpdateReceived(T entity);
        public abstract void DeleteReceived(TId id);
    }
}
