using Paden.ImperfectDollop.Broker.Redis;
using Xunit;
using Xunit.Abstractions;

namespace Paden.ImperfectDollop.Integration.Tests
{
    public class StudentRepositoryWithRedisBrokerTests : StudentRepositoryTests, IClassFixture<DatabaseFixture>
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
