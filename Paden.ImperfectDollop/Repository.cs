using Paden.ImperfectDollop.FallbackStrategies;
using System;
using System.Collections.Generic;

namespace Paden.ImperfectDollop
{
    public abstract class Repository<T, TId> where T : Entity<TId>, new()
    {
        private DateTime? sourceConnectionAliveSince;
        public DateTime? SourceConnectionAliveSince
        {
            get => sourceConnectionAliveSince;
            set
            {
                var oldState = sourceConnectionAliveSince;
                sourceConnectionAliveSince = value;
                if (oldState.HasValue != sourceConnectionAliveSince.HasValue)
                {
                    SourceConnectionStateChanged?.Invoke(this, new SourceConnectionStateChangedEventArgs
                    {
                        IsAlive = sourceConnectionAliveSince.HasValue
                    });
                }
            }
        }
        public bool SourceConnectionIsAlive => SourceConnectionAliveSince.HasValue;

        public DateTime? LastSourceRead { get; protected set; }
        public virtual TimeSpan? ExpiryInterval { get; } = TimeSpan.FromMinutes(2);

        public delegate void EntityEventHandler(object sender, EntityEventArgs<T> a);
        public event EntityEventHandler EntityCreated;
        public event EntityEventHandler EntityUpdated;
        public event EntityEventHandler EntityDeleted;

        public delegate void SourceConnectionStateChangedEventHandler(object sender, SourceConnectionStateChangedEventArgs a);
        public event SourceConnectionStateChangedEventHandler SourceConnectionStateChanged;

        public virtual IFallbackStrategy FallbackStrategy { get; } = new OneRetryThenRPCFallbackStrategy();

        public Func<IEnumerable<T>> FallbackFunction { get; set; }

        public abstract bool IsThreadSafe { get; }
        public abstract ulong ItemCount { get; }

        public virtual IEnumerable<T> GetAll()
        {
            try
            {
                var result = GetAllFromSource();
                LastSourceRead = 
                SourceConnectionAliveSince = DateTime.UtcNow;
                return result;
            }
            catch (Exception ex)
            {
                SourceConnectionAliveSince = null;
                if (FallbackStrategy != null && FallbackFunction != null)
                {
                    return FallbackStrategy.GetAll(this, ex);
                }
                throw;
            }
        }
        protected abstract IEnumerable<T> GetAllFromSource();

        public virtual T Get(TId id)
        {
            try
            {
                var result = GetFromSource(id);
                SourceConnectionAliveSince = DateTime.UtcNow;
                return result;
            }
            catch (Exception)
            {
                SourceConnectionAliveSince = null;
                throw;
            }
        }
        protected abstract T GetFromSource(TId id);

        public virtual StatusCode Create(T entity)
        {
            try
            {
                var result = CreateInSource(entity);
                SourceConnectionAliveSince = DateTime.UtcNow;
                EntityCreated?.Invoke(this, new EntityEventArgs<T>
                {
                    EntityAction = EntityAction.Create,
                    Entity = entity
                });
                return result;
            }
            catch (Exception)
            {
                SourceConnectionAliveSince = null;
                throw;
            }
        }
        protected abstract StatusCode CreateInSource(T entity);

        public virtual StatusCode Update(T entity)
        {
            entity.Version++;
            StatusCode result;
            try
            {
                result = UpdateInSource(entity);
                SourceConnectionAliveSince = DateTime.UtcNow;
                EntityUpdated?.Invoke(this, new EntityEventArgs<T>
                {
                    EntityAction = EntityAction.Update,
                    Entity = entity
                });
            }
            catch (Exception)
            {
                SourceConnectionAliveSince = null;
                entity.Version--;
                throw;
            }
            return result;
        }
        protected abstract StatusCode UpdateInSource(T entity);

        public virtual StatusCode Delete(TId id)
        {
            try
            {
                var result = DeleteInSource(id);
                SourceConnectionAliveSince = DateTime.UtcNow;
                EntityDeleted?.Invoke(this, new EntityEventArgs<T>
                {
                    EntityAction = EntityAction.Delete,
                    Entity = new T { Id = id }
                });
                return result;
            }
            catch (Exception)
            {
                SourceConnectionAliveSince = null;
                throw;
            }
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
