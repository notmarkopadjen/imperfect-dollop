![project icon](Assets/icon-64.png)

# Imperfect Dollop
**This is a .net library that helps user create distributed in-memory cache.**

The library targets [.net core 2.2](https://dotnet.microsoft.com/download/dotnet-core/2.2)

Can be found on [NuGet.org](https://www.nuget.org/packages?q=Paden.ImperfectDollop):
* [Paden.ImperfectDollop](https://www.nuget.org/packages/Paden.ImperfectDollop/) - Base library; provides in-memory cache functionality
* [Paden.ImperfectDollop.Broker.RabbitMQ](https://www.nuget.org/packages/Paden.ImperfectDollop.Broker.RabbitMQ/) - RabbitMQ broker; provides data synchronization functionality over RabbitMQ server
* [Paden.ImperfectDollop.Broker.Redis](https://www.nuget.org/packages/Paden.ImperfectDollop.Broker.Redis/) - Redis broker; provides data synchronization functionality over Redis server
* [Paden.ImperfectDollop.Prometheus](https://www.nuget.org/packages/Paden.ImperfectDollop.Prometheus/) - Prometheus endpoint manager; repository information provider for [prometheus-net](https://www.nuget.org/packages/prometheus-net/)


## What this library is *not*

* Standalone server
* ACID compliant
 
## What this library *is*
* Really fast
* Data source connection fault tolerant
* Able to acquire data from sibling nodes

## Idea

There are requirements of caching commonly accessed data. Usualy we do this by storing the data to some caching server (like Redis) and read it from all the nodes. For most use cases this may be the best approach, and I strongly recommend using that if it fits all of your requirements.

From time to time this is not enough for one of the folowing reasons:
* Connection to caching server is slow, unreliable and / or expensive
* System needs to continue working while cache server is down (interruption or maintenance)
* Caching server does not support complex data operations that are required
* We really don't want to have it over there; we want to have it in our memory

## Implementation

We solve this problem by using in-memory cache.

If just being used by itself (`Paden.ImperfectDollop.Repository<T, TId>` implemented or one of inherited classes `Paden.ImperfectDollop.DictionaryRepository<T, TId>` or `Paden.ImperfectDollop.ConcurrentDictionaryRepository`) it will provide in-memory caching functionality.

If broker is presented (`Paden.ImperfectDollop.IBroker`) it will hook up and provided synchronization features as well.

`Repository<T, TId>` has some tweaking options in order to make it configurable for many use cases:
* `TimeSpan? ExpiryInterval` - default is 2 minutes
* `bool IsReadOnly` - flag indended to be set on slave nodes
* `IFallbackStrategy FallbackStrategy` - isolates fallback decision making actions; provided is `OneRetryThenRPCFallbackStrategy` which is default

It also contains abstract methods which need to be implemented if `Repository<T, TId>` is inherited. If provided distionary repositories are implemented, this is not required.

## Sample project

![sample architecture](Assets/Sample%20architecture.png)

Provided is the project `Paden.SimpleREST`, with can be ran by booting up `docker-compose.yml`.
This compose file includes:
1. Five instances of `Simple REST` **web application**
2. **MariaDb** as relational database
3. **RabbitMQ** as a broker option
4. **Redis** as a broker option

Repository is utilizing provided `ConcurrentDictionaryRepository<T, TId>`:
```C#
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

```

for entity:

```C#
using Paden.ImperfectDollop;

namespace Paden.SimpleREST.Data
{
    public class Student : Entity<int>
    {
        public const string PreferedDatabase = "University";
        public const string ReCreateStatement = @"
DROP TABLE IF EXISTS `Students`;
" + CreateStatement;

        public const string CreateStatement = @"
CREATE TABLE IF NOT EXISTS `Students`  (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) CHARACTER SET latin1 COLLATE latin1_swedish_ci NOT NULL,
  `version` bigint(255) UNSIGNED NULL DEFAULT NULL,
  PRIMARY KEY (`id`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1 CHARACTER SET = latin1 COLLATE = latin1_swedish_ci ROW_FORMAT = Dynamic;
";

        public string Name { get; set; }
    }
}
```

When this implemented, it needs to be added to IoC container:

```C#
// Option 1 - RabbitMQBroker
services.AddSingleton<IBroker, RabbitMQBroker>(sp =>
{
    var settings = sp.GetService<IOptions<Settings>>();
    return new RabbitMQBroker(settings.Value.RabbitMQ, sp.GetService<ILogger<RabbitMQBroker>>());
});

// Option 2 - RedisBroker
//services.AddSingleton<IBroker, RedisBroker>(sp =>
//{
//    var settings = sp.GetService<IOptions<Settings>>();
//    return new RedisBroker(settings.Value.Redis, sp.GetService<ILogger<RedisBroker>>());
//});
services.AddSingleton<StudentRepository>();
```

and it is ready for being used in controller:

```C#
using Microsoft.AspNetCore.Mvc;
using Paden.SimpleREST.Data;
using System.Collections.Generic;

namespace Paden.SimpleREST.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StudentsController : ControllerBase
    {
        private readonly StudentRepository studentRepository;

        public StudentsController(StudentRepository studentRepository)
        {
            this.studentRepository = studentRepository;
        }

        [HttpGet]
        public IEnumerable<Student> Get()
        {
            return studentRepository.GetAll();
        }

        [HttpGet("{id}")]
        public Student Get(int id)
        {
            return studentRepository.Get(id);
        }

        [HttpPost]
        public void Post([FromBody] Student value)
        {
            studentRepository.Create(value);
        }

        [HttpPut("{id}")]
        public void Put(int id, [FromBody] Student value)
        {
            value.Id = id;
            studentRepository.Update(value);
        }

        [HttpDelete("{id}")]
        public void Delete(int id)
        {
            studentRepository.Delete(id);
        }

        [HttpGet("info")]
        public RepositoryInfo GetInfo()
        {
            return new RepositoryInfo
            {
                EntitiesCount = studentRepository.ItemCount,
                LastSourceRead = studentRepository.LastSourceRead,
                SourceConnectionAliveSince = studentRepository.SourceConnectionAliveSince
            };
        }
    }
}

```

Optionally, you can register Prometheus endpoint by booting up `prometheus-net`:
```C#
public void Configure(IApplicationBuilder app, IHostingEnvironment env)
{
    // ...
    app.UseMetricServer();
    // ...
}
```
and adding repository metrics:
```C#
services.AddSingleton(sp => new RepositoryMetrics<StudentRepository, Student, int>(sp.GetService<StudentRepository>()));
services.BuildServiceProvider().GetService<RepositoryMetrics<StudentRepository, Student, int>>();
```

which then produces result like this:

```
# HELP repositories_studentrepository_source_connection_age_seconds Time in seconds since current source connection has been established
# TYPE repositories_studentrepository_source_connection_age_seconds gauge
repositories_studentrepository_source_connection_age_seconds 19.2369868
# HELP repositories_studentrepository_data_age_seconds Time in seconds since last read from source
# TYPE repositories_studentrepository_data_age_seconds gauge
repositories_studentrepository_data_age_seconds 19.2369857
# HELP repositories_studentrepository_entities_count Number of entities loaded in repository
# TYPE repositories_studentrepository_entities_count gauge
repositories_studentrepository_entities_count 2
# HELP process_private_memory_bytes Process private memory size
# TYPE process_private_memory_bytes gauge
process_private_memory_bytes 0
# HELP dotnet_collection_count_total GC collection count
# TYPE dotnet_collection_count_total counter
dotnet_collection_count_total{generation="1"} 0
dotnet_collection_count_total{generation="0"} 0
dotnet_collection_count_total{generation="2"} 0
# HELP process_num_threads Total number of threads
# TYPE process_num_threads gauge
process_num_threads 26
# HELP process_cpu_seconds_total Total user and system CPU time spent in seconds.
# TYPE process_cpu_seconds_total counter
process_cpu_seconds_total 3.87
# HELP dotnet_total_memory_bytes Total known allocated memory
# TYPE dotnet_total_memory_bytes gauge
dotnet_total_memory_bytes 11666328
# HELP process_start_time_seconds Start time of the process since unix epoch in seconds.
# TYPE process_start_time_seconds gauge
process_start_time_seconds 1566706242.23
# HELP process_virtual_memory_bytes Virtual memory size in bytes.
# TYPE process_virtual_memory_bytes gauge
process_virtual_memory_bytes 12289839104
# HELP process_working_set_bytes Process working set
# TYPE process_working_set_bytes gauge
process_working_set_bytes 98308096
# HELP process_open_handles Number of open handles
# TYPE process_open_handles gauge
process_open_handles 210
```

on endpoint `http://localhost:5081/metrics`.


## Known limitations and advises

Applications using this library can be ran in *master-master* or *master-slave* mode. First one, although possible, is not recommended because it may lead to data inconsistency. You should be aware of this. This library is not ACID and doesn't handle data conflicts. It has simple version checking, but on heavy load it is not enough.

More common usage is *master-slave*, where, for example, we have web admin application which can change data, and many nodes (eg microservices) which utilize this data. This library was made for scenarios like this, and it handles them very well. In order to ensure slave nodes are not changing data, you can set `IsReadOnly` property on repository on slave nodes.

For synchronization purposes, you may provide any `Paden.ImperfectDollop.IBroker`. The ones provided are **RabbitMQ** and **Redis**, but you are welcome to implement additional ones by using provided ones as an example.

### RabbitMQ broker

**RabbitMQ** is a message bus, so it is made for scenarios like this one. Entity change events are being propagated over fanout exchange to client-reading queues. RPC (remote procedure call) is execute over private channels.

Should be used if possible.

### Redis broker

**Redis** is key-value pair caching server. It provides queueing and stacking functionality, but it is pull-only. So, RPC listeners have to maintain their thread and read from Redis queue occasionally.

Should be used if **RabbitMQ** is not possible, **Redis** server is already in place, or fits your use case better.

## Plans for next releases
* Entity Framework Core provider