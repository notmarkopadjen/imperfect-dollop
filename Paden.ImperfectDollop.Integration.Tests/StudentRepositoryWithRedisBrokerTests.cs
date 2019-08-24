using Paden.ImperfectDollop.Broker.Redis;
using Xunit.Abstractions;

namespace Paden.ImperfectDollop.Integration.Tests
{
    public class StudentRepositoryWithRedisBrokerTests : StudentRepositoryTests
    {
        public StudentRepositoryWithRedisBrokerTests(DatabaseFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }

        protected override IBroker CreateBroker()
        {
            return new RedisBroker();
        }
    }
}
