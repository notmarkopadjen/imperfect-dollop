﻿using Paden.ImperfectDollop.FallbackStrategies;
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
        public virtual bool IsReadOnly { get; protected set; }

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

        public Repository(IBroker broker = null)
        {
            if (broker != null)
            {
                broker.ListenFor(this);
                if (broker.SupportsRemoteProcedureCall)
                {
                    broker.StartRPC(this);
                }
            }
        }

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

        public virtual void Create(T entity)
        {
            try
            {
                CreateInSource(entity);
                SourceConnectionAliveSince = DateTime.UtcNow;
                EntityCreated?.Invoke(this, new EntityEventArgs<T>
                {
                    EntityAction = EntityAction.Create,
                    Entity = entity
                });
                CreateReceived(entity);
            }
            catch (Exception)
            {
                SourceConnectionAliveSince = null;
                throw;
            }
        }
        protected abstract void CreateInSource(T entity);

        public virtual void Update(T entity)
        {
            entity.Version++;
            try
            {
                UpdateInSource(entity);
                SourceConnectionAliveSince = DateTime.UtcNow;
                EntityUpdated?.Invoke(this, new EntityEventArgs<T>
                {
                    EntityAction = EntityAction.Update,
                    Entity = entity
                });
                UpdateReceived(entity);
            }
            catch (Exception)
            {
                SourceConnectionAliveSince = null;
                entity.Version--;
                throw;
            }
        }
        protected abstract void UpdateInSource(T entity);

        public virtual void Delete(TId id)
        {
            try
            {
                DeleteInSource(id);
                SourceConnectionAliveSince = DateTime.UtcNow;
                EntityDeleted?.Invoke(this, new EntityEventArgs<T>
                {
                    EntityAction = EntityAction.Delete,
                    Entity = new T { Id = id }
                });
                DeleteReceived(id);
            }
            catch (Exception)
            {
                SourceConnectionAliveSince = null;
                throw;
            }
        }
        protected abstract void DeleteInSource(TId id);

        public abstract void CreateReceived(T entity);
        public abstract void UpdateReceived(T entity);
        public abstract void DeleteReceived(TId id);
    }
}
