using Microsoft.Extensions.Configuration;
using Moq;
using MySql.Data.MySqlClient;
using Paden.ImperfectDollop.Integration.Tests.TestSystem;
using System;
using System.Data;

namespace Paden.ImperfectDollop.Integration.Tests
{
    public class DatabaseFixture : IDisposable
    {
        public MySqlConnection Connection { get; private set; }
        public readonly string DatabaseName = $"integration_test_{Guid.NewGuid():N}";
        public string ConnectionString { get; private set; }

        public DatabaseFixture()
        {
            var config = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, false).AddEnvironmentVariables().Build();
            var connectionString = config.GetConnectionString("DefaultConnection");
            Connection = new MySqlConnection(connectionString);

            Connection.Open();
            new MySqlCommand($"CREATE DATABASE `{DatabaseName}`;", Connection).ExecuteNonQuery();
            Connection.ChangeDatabase(DatabaseName);

            ConnectionString = $"{connectionString};Database={DatabaseName}";
        }

        public void RecreateTables()
        {
            new MySqlCommand(Student.ReCreateStatement, Connection).ExecuteNonQuery();
        }

        public IDbConnection GetConnectionFacade()
        {
            var connectionMock = Mock.Of<IDbConnection>();
            Mock.Get(connectionMock).Setup(m => m.CreateCommand()).Returns(Connection.CreateCommand()).Verifiable();
            Mock.Get(connectionMock).SetupGet(m => m.State).Returns(ConnectionState.Open).Verifiable();
            return connectionMock;
        }

        public void Dispose()
        {
            try
            {
                new MySqlCommand($"DROP DATABASE IF EXISTS `{DatabaseName}`;", Connection).ExecuteNonQuery();
            }
            catch (Exception)
            {
                // ignored
            }
            Connection.Close();
        }
    }
}
