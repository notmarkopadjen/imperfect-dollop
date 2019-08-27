using Microsoft.Extensions.Logging;
using Paden.ImperfectDollop.FallbackStrategies;
using System;
using System.Collections.Generic;

namespace Paden.ImperfectDollop
{
    public abstract class Repository<T, TId> where T : Entity<TId>, new()
    {
        public virtual TimeSpan? ExpiryInterval { get; } = TimeSpan.FromMinutes(2);
        public virtual bool IsReadOnly { get; protected set; } = false;
        public virtual IFallbackStrategy FallbackStrategy => new OneRetryThenRPCFallbackStrategy(logger);

        public abstract bool IsThreadSafe { get; }
        public abstract ulong ItemCount { get; }

        public Func<IEnumerable<T>> FallbackFunction { get; set; }
        public DateTime? LastSourceRead { get; protected set; }

        private DateTime? sourceConnectionAliveSince;
        protected readonly ILogger logger;

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

        public delegate void EntityEventHandler(object sender, EntityEventArgs<T> a);
        public event EntityEventHandler EntityCreated;
        public event EntityEventHandler EntityUpdated;
        public event EntityEventHandler EntityDeleted;

        public delegate void SourceConnectionStateChangedEventHandler(object sender, SourceConnectionStateChangedEventArgs a);
        public event SourceConnectionStateChangedEventHandler SourceConnectionStateChanged;


        public Repository(ILogger logger = null, IBroker broker = null)
        {
            this.logger = logger;
            if (broker != null)
            {
                logger?.LogTrace("Broker provided. Starting listener");
                broker.ListenFor(this);
                if (broker.SupportsRemoteProcedureCall)
                {
                    logger?.LogTrace("Broker supports RPC. Starting RPC listener");
                    broker.StartRPC(this);
                }
            }
        }

        public virtual IEnumerable<T> GetAll()
        {
            try
            {
                logger?.LogTrace("Reading all from source");
                var result = GetAllFromSource();
                LastSourceRead =
                SourceConnectionAliveSince = DateTime.UtcNow;
                return result;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Exception on GetAll");
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
                logger?.LogTrace("Reading by id {0} from source", id);
                var result = GetFromSource(id);
                SourceConnectionAliveSince = DateTime.UtcNow;
                return result;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Exception on Get({0})", id);
                SourceConnectionAliveSince = null;
                throw;
            }
        }
        protected abstract T GetFromSource(TId id);

        public virtual void Create(T entity)
        {
            if (IsReadOnly)
            {
                throw new ReadOnlyException("Cannot create new entity in read-only repository");
            }
            try
            {
                logger?.LogTrace("Creating entity {0}", entity);
                CreateInSource(entity);
                SourceConnectionAliveSince = DateTime.UtcNow;
                CreateReceived(entity);
                EntityCreated?.Invoke(this, new EntityEventArgs<T>
                {
                    EntityAction = EntityAction.Create,
                    Entity = entity
                });
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Exception on Create({0})", entity);
                SourceConnectionAliveSince = null;
                throw;
            }
        }
        protected abstract void CreateInSource(T entity);

        public virtual void Update(T entity)
        {
            if (IsReadOnly)
            {
                throw new ReadOnlyException("Cannot update entity in read-only repository");
            }
            entity.Version++;
            try
            {
                logger?.LogTrace("Updating entity {0}", entity);
                UpdateInSource(entity);
                SourceConnectionAliveSince = DateTime.UtcNow;
                UpdateReceived(entity);
                EntityUpdated?.Invoke(this, new EntityEventArgs<T>
                {
                    EntityAction = EntityAction.Update,
                    Entity = entity
                });
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Exception on Update({0})", entity);
                SourceConnectionAliveSince = null;
                entity.Version--;
                throw;
            }
        }
        protected abstract void UpdateInSource(T entity);

        public virtual void Delete(TId id)
        {
            if (IsReadOnly)
            {
                throw new ReadOnlyException("Cannot delete entity in read-only repository");
            }
            try
            {
                logger?.LogTrace("Deleting by id {0} from source", id);
                DeleteInSource(id);
                SourceConnectionAliveSince = DateTime.UtcNow;
                DeleteReceived(id);
                EntityDeleted?.Invoke(this, new EntityEventArgs<T>
                {
                    EntityAction = EntityAction.Delete,
                    Entity = new T { Id = id }
                });
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Exception on Delete({0})", id);
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
