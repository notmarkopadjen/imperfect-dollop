using Microsoft.Extensions.Configuration;
using Paden.ImperfectDollop.Broker.Redis;
using Xunit;
using Xunit.Abstractions;

namespace Paden.ImperfectDollop.Integration.Tests
{
    public class StudentRepositoryWithRedisBrokerTests : StudentRepositoryTests, IClassFixture<DatabaseFixture>
    {
        string uri;
        public StudentRepositoryWithRedisBrokerTests(DatabaseFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
            uri = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, false).AddEnvironmentVariables().Build()["Redis:Uri"];
        }

        protected override IBroker CreateBroker()
        {
            return new RedisBroker(uri);
        }
    }
}
