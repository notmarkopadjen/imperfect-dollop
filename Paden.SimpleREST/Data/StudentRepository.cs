using Dapper.Contrib.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;
using Paden.ImperfectDollop;
using System;
using System.Collections.Generic;
using System.Data;

namespace Paden.SimpleREST.Data
{
    public class StudentRepository : ConcurrentDictionaryRepository<Student, int>
    {
        private readonly string connectionString;

        public StudentRepository(IOptions<Settings> settings, ILogger<StudentRepository> logger = null, IBroker broker = null) : base(logger, broker)
        {
            connectionString = settings.Value.Database;
            ExecuteStatement($"CREATE DATABASE IF NOT EXISTS `{Student.PreferedDatabase}`");
            connectionString = $"{connectionString};Database={Student.PreferedDatabase}";
            ExecuteStatement(Student.CreateStatement);
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
            using (IDbConnection db = new MySqlConnection(connectionString))
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
