using Paden.ImperfectDollop.Broker.RabbitMQ;
using Xunit.Abstractions;

namespace Paden.ImperfectDollop.Integration.Tests
{
    public class StudentRepositoryWithRabbitMQBrokerTests : StudentRepositoryTests
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
