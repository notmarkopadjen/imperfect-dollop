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
