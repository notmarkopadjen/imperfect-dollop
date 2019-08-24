using Microsoft.Extensions.Configuration;
using Paden.ImperfectDollop.Broker.RabbitMQ;
using Xunit;
using Xunit.Abstractions;

namespace Paden.ImperfectDollop.Integration.Tests
{
    public class StudentRepositoryWithRabbitMQBrokerTests : StudentRepositoryTests, IClassFixture<DatabaseFixture>
    {
        string uri;
        public StudentRepositoryWithRabbitMQBrokerTests(DatabaseFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
            uri = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, false).AddEnvironmentVariables().Build()["RabbitMQ:Uri"];
        }

        protected override IBroker CreateBroker()
        {
            return new RabbitMQBroker(uri);
        }
    }
}
