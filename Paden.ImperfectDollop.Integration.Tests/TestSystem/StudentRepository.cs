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
        
        protected override StatusCode CreateInSource(Student entity)
        {
            return WithConnection(db => ExecuteHandled(() => db.Insert(entity)));
        }

        protected override StatusCode DeleteInSource(int id)
        {
            return WithConnection(db => ExecuteHandled(() => db.Delete(new Student { Id = id })));
        }

        protected override IEnumerable<Student> GetAllFromSource()
        {
            return WithConnection(db => db.GetAll<Student>());
        }

        protected override StatusCode UpdateInSource(Student entity)
        {
            return WithConnection(db => ExecuteHandled(() => db.Update(entity)));
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
