using Paden.ImperfectDollop.Broker.RabbitMQ;
using Xunit;
using Xunit.Abstractions;

namespace Paden.ImperfectDollop.Integration.Tests
{
    public class StudentRepositoryWithRabbitMQBrokerTests : StudentRepositoryTests, IClassFixture<DatabaseFixture>
    {
        public StudentRepositoryWithRabbitMQBrokerTests(DatabaseFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }

        protected override IBroker CreateBroker()
        {
            return new RabbitMQBroker();
        }
    }
}
