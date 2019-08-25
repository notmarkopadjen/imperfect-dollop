using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

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
        // GET api/values
        [HttpGet]
        public IEnumerable<Student> Get()
        {
            return studentRepository.GetAll();
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public Student Get(int id)
        {
            return studentRepository.Get(id);
        }

        // POST api/values
        [HttpPost]
        public void Post([FromBody] Student value)
        {
            studentRepository.Create(value);
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] Student value)
        {
            value.Id = id;
            studentRepository.Update(value);
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
            studentRepository.Delete(id);
        }
    }
}
