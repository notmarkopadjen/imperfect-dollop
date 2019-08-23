using Paden.ImperfectDollop.Broker.RabbitMQ;
using Paden.ImperfectDollop.Integration.Tests.TestSystem;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Paden.ImperfectDollop.Integration.Tests
{
    public class StudentRepositoryTests
    {
        const int studentId = 1;

        private readonly ITestOutputHelper output;

        StudentRepository systemUnderTest;
        RabbitMQBroker broker;

        public StudentRepositoryTests(ITestOutputHelper output)
        {
            systemUnderTest = new StudentRepository();
            systemUnderTest.ExecuteStatement(Student.ReCreateStatement);

            broker = new RabbitMQBroker();
            broker.StartFor(systemUnderTest);

            this.output = output;
        }

        [Fact]
        public void GetAll_Should_Return_Entities()
        {
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

        [Fact]
        public void Repository_Should_Create_Update_Delete_Entity_And_Return_Proper_Results_Using_Database_And_Cache_On_All_Connected_Repositories()
        {
            var repository2 = new StudentRepository();
            broker.StartFor(repository2);

            systemUnderTest.Create(new Student
            {
                Name = "Not Marko Padjen"
            });

            string newName;

            systemUnderTest.Update(new Student
            {
                Id = studentId,
                Name = newName = $"Name {DateTime.Now}"
            });

            // Checking if database calls return updated name
            Assert.Equal(newName, systemUnderTest.GetAll().First().Name);
            Thread.Sleep(100);
            Assert.Equal(newName, repository2.GetAll().First().Name);

            systemUnderTest.Delete(studentId);

            // Checking if database table is empty after entity deletion
            Assert.False(systemUnderTest.GetAll().Any());
        }
    }
}
