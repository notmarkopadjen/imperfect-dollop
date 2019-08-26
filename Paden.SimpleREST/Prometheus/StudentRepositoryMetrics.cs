using Paden.SimpleREST.Data;
using Prometheus;
using System;

namespace Paden.SimpleREST.Prometheus
{
    public class StudentRepositoryMetrics
    {
        private static readonly Gauge entitiesCount = Metrics.CreateGauge("repositories_students_entities_count", "Number of entities loaded in repository");
        private static readonly Gauge dataOldSeconds = Metrics.CreateGauge("repositories_students_data_age_seconds", "Time in seconds since last read from source");
        private static readonly Gauge sourceConnectionLiveSeconds = Metrics.CreateGauge("repositories_students_source_connection_age_seconds", "Time in seconds since current source connection has been established");

        public StudentRepositoryMetrics(StudentRepository repository)
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
