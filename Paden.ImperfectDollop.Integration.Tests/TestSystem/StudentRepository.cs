using Dapper.Contrib.Extensions;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;

namespace Paden.ImperfectDollop.Integration.Tests.TestSystem
{
    public class StudentRepository : ConcurrentDictionaryRepository<Student, int>
    {
        public readonly string ConnectionString = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", false, false)
            .AddEnvironmentVariables()
            .Build().GetConnectionString("DefaultConnection");

        public StudentRepository(IBroker broker = null) : base(broker)
        {

        }

        protected override void CreateInSource(Student entity)
        {
            WithConnection(db => db.Insert(entity));
        }

        protected override void DeleteInSource(int id)
        {
            WithConnection(db => db.Delete(new Student { Id = id }));
        }

        protected override IEnumerable<Student> GetAllFromSource()
        {
            return WithConnection(db => db.GetAll<Student>());
        }

        protected override void UpdateInSource(Student entity)
        {
            WithConnection(db => db.Update(entity));
        }

        public T WithConnection<T>(Func<IDbConnection, T> function)
        {
            using (IDbConnection db = new MySqlConnection(ConnectionString))
            {
                db.Open();
                return function(db);
            }
        }

        public int ExecuteStatement(string sql)
        {
            return WithConnection(db => new MySqlCommand(sql, db as MySqlConnection).ExecuteNonQuery());
        }
    }
}
