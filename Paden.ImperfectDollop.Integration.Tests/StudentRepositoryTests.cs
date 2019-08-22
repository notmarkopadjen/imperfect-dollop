using Paden.ImperfectDollop.Integration.Tests.TestSystem;
using System;
using System.Diagnostics;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Paden.ImperfectDollop.Integration.Tests
{
    public class StudentRepositoryTests
    {
        const int studentId = 1;

        private readonly ITestOutputHelper output;
        StudentRepository systemUnderTest;

        public StudentRepositoryTests(ITestOutputHelper output)
        {
            systemUnderTest = new StudentRepository();
            systemUnderTest.ExecuteStatement(Student.ReCreateStatement);

            this.output = output;
        }

        [Fact]
        public void GetAll_Should_Return_Entities()
        {
            var sw = new Stopwatch();
            sw.Start();
            Assert.True(systemUnderTest.GetAll().ToArray().Length > 0);
            sw.Stop();

            output.WriteLine($"Read run time (ms): {sw.ElapsedMilliseconds}");
        }

        [Fact]
        public void Repository_Should_Create_Update_Delete_Entity_And_Return_Proper_Results_Using_Database_And_Cache()
        {
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

            systemUnderTest.Delete(studentId);

            // Checking if database table is empty after entity deletion
            Assert.False(systemUnderTest.GetAll().Any());
        }
    }
}
