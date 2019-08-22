using Dapper.Contrib.Extensions;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Data;

namespace Paden.ImperfectDollop.Integration.Tests.TestSystem
{
    public class StudentRepository : DictionaryRepository<Student, int>
    {
        static readonly string connectionString = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, false).Build().GetConnectionString("DefaultConnection");

        protected override StatusCode CreateInSource(Student entity)
        {
            using (IDbConnection db = new MySqlConnection(connectionString))
            {
                return ExecuteHandled(() => db.Insert(entity));
            }
        }

        protected override StatusCode DeleteInSource(int id)
        {
            using (IDbConnection db = new MySqlConnection(connectionString))
            {
                return ExecuteHandled(() => db.Delete(new Student { Id = id }));
            }
        }

        protected override IEnumerable<Student> GetAllFromSource()
        {
            using (IDbConnection db = new MySqlConnection(connectionString))
            {
                return db.GetAll<Student>();
            }
        }

        protected override StatusCode UpdateInSource(Student entity)
        {
            using (IDbConnection db = new MySqlConnection(connectionString))
            {
                return ExecuteHandled(() => db.Update(entity));
            }
        }
    }
}
