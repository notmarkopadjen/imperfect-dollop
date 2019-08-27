using Dapper.Contrib.Extensions;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;

namespace Paden.ImperfectDollop.Integration.Tests.TestSystem
{
    public class StudentRepository : ConcurrentDictionaryRepository<Student, int>
    {
        private readonly string connectionString;

        public StudentRepository(string connectionString, ILogger<StudentRepository> logger = null, IBroker broker = null) : base(logger, broker)
        {
            this.connectionString = connectionString;
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
