using Paden.ImperfectDollop.Integration.Tests.TestSystem;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Paden.ImperfectDollop.Integration.Tests
{
    public abstract class StudentRepositoryTests
    {
        const int studentId = 1;

        TimeSpan propagationTolerance = TimeSpan.FromSeconds(2);

        private readonly DatabaseFixture fixture;
        private readonly ITestOutputHelper output;

        public StudentRepositoryTests(DatabaseFixture fixture, ITestOutputHelper output)
        {
            this.fixture = fixture;
            this.output = output;

            fixture.RecreateTables();
        }

        protected abstract IBroker CreateBroker();

        [Fact]
        public void Should_Return_Entities_When_Asked_For()
        {
            using (var broker = CreateBroker())
            {
                var systemUnderTest = new StudentRepository(fixture.ConnectionString, null, broker);
                systemUnderTest.Create(new Student
                {
                    Name = "Not Marko Padjen"
                });

                var sw = new Stopwatch();
                sw.Start();
                Assert.True(systemUnderTest.GetAll().ToArray().Length > 0);
                sw.Stop();

                output.WriteLine($"Read run time (ms): {sw.ElapsedMilliseconds}");
            }
        }

        [Fact]
        public void Should_Create_Update_Delete_Entity_And_Return_Proper_Results_Using_Database_And_Cache_On_All_Connected_Repositories()
        {
            using (var broker = CreateBroker())
            using (var broker2 = CreateBroker())
            {
                var repository = new StudentRepository(fixture.ConnectionString, null, broker);
                var repository2 = new StudentRepository(fixture.ConnectionString, null, broker2);

                repository.Create(new Student
                {
                    Name = "Not Marko Padjen"
                });

                string newName;

                repository.Update(new Student
                {
                    Id = studentId,
                    Name = newName = $"Name {DateTime.Now}"
                });

                // Checking if database calls return updated name
                Assert.Equal(newName, repository.GetAll().First().Name);
                Thread.Sleep(100);
                Assert.Equal(newName, repository2.GetAll().First().Name);

                repository.Delete(studentId);
                // Checking if database table is empty after entity deletion
                Assert.False(repository.GetAll().Any());
            }
        }

        [Fact]
        public void Should_Propagate_Inserts_Properly()
        {
            const int systemsCount = 10;
            const int entitiesCount = 10_000;

            // Data seed
            new StudentRepository(fixture.ConnectionString).ExecuteStatement("insert into Students(`name`, `version`) select CONCAT('Name ', seq) AS `name`, 0 from seq_1_to_" + entitiesCount);

            // Systems init
            var brokerBag = new IBroker[systemsCount];
            for (int i = 0; i < systemsCount; i++)
            {
                brokerBag[i] = CreateBroker();
            }

            var repositoryBag = Enumerable.Range(0, systemsCount).Select(l => new StudentRepository(fixture.ConnectionString, null, brokerBag[l])).ToArray();
            var masterRepository = repositoryBag.First();


            void AssertAllRepositories(Predicate<StudentRepository> predicate)
            {
                var failed = repositoryBag.AsParallel().FirstOrDefault(l =>
                {
                    var startTime = DateTime.UtcNow;
                    bool result = predicate(l);
                    while (!result && (DateTime.UtcNow - startTime) < propagationTolerance)
                    {
                        result = predicate(l);
                    }
                    return !result;
                });
                Assert.True(failed == null, $"Repository {failed} is not null.");
            }

            try
            {
                // Verify all have all records loaded
                AssertAllRepositories(r => r.ItemCount == entitiesCount);

                // Insert new 100 items with name "100" using random repositories
                var rand = new Random();
                Parallel.For(10_001, 10_101, l =>
                {
                    repositoryBag[rand.Next(systemsCount - 1)].Create(new Student
                    {
                        Name = "100"
                    });
                });
                AssertAllRepositories(r => r.GetAll().Count(l => l.Id > 10_000) == 100);
            }
            finally
            {
                // Dispose
                for (int i = 0; i < systemsCount; i++)
                {
                    brokerBag[i].Dispose();
                }
            }
        }

        [Fact]
        public void Should_Propagate_Updates_Properly()
        {
            const int systemsCount = 10;
            const int entitiesCount = 10_000;

            // Data seed
            new StudentRepository(fixture.ConnectionString).ExecuteStatement("insert into Students(`name`, `version`) select CONCAT('Name ', seq) AS `name`, 0 from seq_1_to_" + entitiesCount);

            // Systems init
            var brokerBag = new IBroker[systemsCount];
            for (int i = 0; i < systemsCount; i++)
            {
                brokerBag[i] = CreateBroker();
            }

            var repositoryBag = Enumerable.Range(0, systemsCount).Select(l => new StudentRepository(fixture.ConnectionString, null, brokerBag[l])).ToArray();
            var masterRepository = repositoryBag.First();

            void AssertAllRepositories(Predicate<StudentRepository> predicate)
            {
                var failed = repositoryBag.AsParallel().FirstOrDefault(l =>
                {
                    var startTime = DateTime.UtcNow;
                    bool result = predicate(l);
                    while (!result && (DateTime.UtcNow - startTime) < propagationTolerance)
                    {
                        result = predicate(l);
                    }
                    return !result;
                });
                Assert.True(failed == null, $"Repository {failed} is not null.");
            }

            try
            {
                // Verify all have all records loaded
                AssertAllRepositories(r => r.ItemCount == entitiesCount);

                // Set name "100" to first 100 entities using random repositories
                var rand = new Random();
                Parallel.ForEach(masterRepository.GetAll().Take(100), l =>
                {
                    l.Name = "100";
                    repositoryBag[rand.Next(systemsCount - 1)].Update(l);
                });
                AssertAllRepositories(r => r.GetAll().Count(l => l.Name == "100") == 100);
            }
            finally
            {
                // Dispose
                for (int i = 0; i < systemsCount; i++)
                {
                    brokerBag[i].Dispose();
                }
            }
        }

        [Fact]
        public void Should_Propagate_Deletes_Properly()
        {
            const int systemsCount = 10;
            const int entitiesCount = 10_000;

            // Data seed
            new StudentRepository(fixture.ConnectionString).ExecuteStatement("insert into Students(`name`, `version`) select CONCAT('Name ', seq) AS `name`, 0 from seq_1_to_" + entitiesCount);

            // Systems init
            var brokerBag = new IBroker[systemsCount];
            for (int i = 0; i < systemsCount; i++)
            {
                brokerBag[i] = CreateBroker();
            }

            var repositoryBag = Enumerable.Range(0, systemsCount).Select(l => new StudentRepository(fixture.ConnectionString, null, brokerBag[l])).ToArray();
            var masterRepository = repositoryBag.First();

            void AssertAllRepositories(Predicate<StudentRepository> predicate)
            {
                var failed = repositoryBag.AsParallel().FirstOrDefault(l =>
                {
                    var startTime = DateTime.UtcNow;
                    bool result = predicate(l);
                    while (!result && (DateTime.UtcNow - startTime) < propagationTolerance)
                    {
                        result = predicate(l);
                    }
                    return !result;
                });
                Assert.True(failed == null, $"Repository {failed} is not null.");
            }

            try
            {
                // Verify all have all records loaded
                AssertAllRepositories(r => r.ItemCount == entitiesCount);

                // Snap fingers
                var rand = new Random();
                Parallel.ForEach(masterRepository.GetAll().Where(i => i.Id % 2 == 0), l =>
                {
                    repositoryBag[rand.Next(systemsCount - 1)].Delete(l.Id);
                });
                AssertAllRepositories(r => r.ItemCount == entitiesCount / 2);
            }
            finally
            {
                // Dispose
                for (int i = 0; i < systemsCount; i++)
                {
                    brokerBag[i].Dispose();
                }
            }
        }

        [Fact]
        public void Should_Return_Data_On_Source_Failure_And_Restart()
        {
            const int systemsCount = 10;
            const int entitiesCount = 10_000;

            // Data seed
            new StudentRepository(fixture.ConnectionString).ExecuteStatement("insert into Students(`name`, `version`) select CONCAT('Name ', seq) AS `name`, 0 from seq_1_to_" + entitiesCount);

            // Systems init
            var brokerBag = new IBroker[systemsCount];
            for (int i = 0; i < systemsCount; i++)
            {
                brokerBag[i] = CreateBroker();
            }

            var repositoryBag = Enumerable.Range(0, systemsCount).Select(l => new StudentRepository(fixture.ConnectionString, null, brokerBag[l])).ToArray();
            var masterRepository = repositoryBag.First();

            void AssertAllRepositories(Predicate<StudentRepository> predicate)
            {
                var failed = repositoryBag.AsParallel().FirstOrDefault(l =>
                {
                    var startTime = DateTime.UtcNow;
                    bool result = predicate(l);
                    while (!result && (DateTime.UtcNow - startTime) < propagationTolerance)
                    {
                        result = predicate(l);
                    }
                    return !result;
                });
                Assert.True(failed == null, $"Repository {failed} is not null.");
            }

            try
            {
                // Verify all have all records loaded
                AssertAllRepositories(r => r.ItemCount == entitiesCount);

                // Rename table
                masterRepository.ExecuteStatement("RENAME TABLE `Students` to `NotStudents`;");

                // Restart half
                Parallel.ForEach(Enumerable.Range(0, systemsCount).Where(i => i % 2 == 0), i =>
                {
                    brokerBag[i].Dispose();
                    brokerBag[i] = CreateBroker();
                    repositoryBag[i] = new StudentRepository(fixture.ConnectionString, null, brokerBag[i]);
                });
                AssertAllRepositories(r => r.ItemCount == entitiesCount);
            }
            finally
            {
                // Dispose
                for (int i = 0; i < systemsCount; i++)
                {
                    brokerBag[i].Dispose();
                }
            }
        }
    }
}
