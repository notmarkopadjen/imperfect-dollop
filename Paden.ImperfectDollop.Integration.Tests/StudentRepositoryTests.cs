using Paden.ImperfectDollop.Integration.Tests.TestSystem;
using System.Linq;
using Xunit;

namespace Paden.ImperfectDollop.Integration.Tests
{
    public class StudentRepositoryTests
    {
        StudentRepository systemUnderTest;

        public StudentRepositoryTests()
        {
            systemUnderTest = new StudentRepository();
        }

        [Fact]
        public void GetAll_Should_Return_Entities()
        {
            Assert.True(systemUnderTest.GetAll().ToArray().Length > 0);
        }
    }
}
