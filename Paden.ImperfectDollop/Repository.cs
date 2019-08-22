using System;
using System.Collections.Generic;

namespace Paden.ImperfectDollop
{
    public abstract class Repository<T, TId> where T : Entity<TId>
    {
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
            return CreateInSource(entity);
        }
        protected abstract StatusCode CreateInSource(T entity);

        public virtual StatusCode Update(T entity)
        {
            return UpdateInSource(entity);
        }
        protected abstract StatusCode UpdateInSource(T entity);

        public virtual StatusCode Delete(TId id)
        {
            return DeleteInSource(id);
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
    }
}
