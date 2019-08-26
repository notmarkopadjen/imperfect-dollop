using System;

namespace Paden.SimpleREST
{
    public class RepositoryInfo
    {
        public ulong EntitiesCount { get; set; }
        public DateTime? LastSourceRead { get; set; }
        public DateTime? SourceConnectionAliveSince { get; set; }
    }
}
