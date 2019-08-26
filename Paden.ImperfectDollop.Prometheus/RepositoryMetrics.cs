using Prometheus;
using System;

namespace Paden.ImperfectDollop.Prometheus
{
    public class RepositoryMetrics<TR, T, TId>
        where TR : Repository<T, TId>
        where T : Entity<TId>, new()
    {
        private static string repositoryName = typeof(TR).Name.ToLower();

        private static readonly Gauge entitiesCount = Metrics.CreateGauge($"repositories_{repositoryName}_entities_count", "Number of entities loaded in repository");
        private static readonly Gauge dataOldSeconds = Metrics.CreateGauge($"repositories_{repositoryName}_data_age_seconds", "Time in seconds since last read from source");
        private static readonly Gauge sourceConnectionLiveSeconds = Metrics.CreateGauge($"repositories_{repositoryName}_source_connection_age_seconds", "Time in seconds since current source connection has been established");

        public RepositoryMetrics(Repository<T, TId> repository)
        {
            Metrics.DefaultRegistry.AddBeforeCollectCallback(() =>
            {
                entitiesCount.Set(repository.ItemCount);
                if (repository.LastSourceRead.HasValue)
                {
                    dataOldSeconds.Set((DateTime.UtcNow - repository.LastSourceRead.Value).TotalSeconds);
                }
                else
                {
                    dataOldSeconds.Set(double.NaN);
                }
                if (repository.SourceConnectionAliveSince.HasValue)
                {
                    sourceConnectionLiveSeconds.Set((DateTime.UtcNow - repository.SourceConnectionAliveSince.Value).TotalSeconds);
                }
                else
                {
                    sourceConnectionLiveSeconds.Set(double.NaN);
                }
            });
        }
    }
}
