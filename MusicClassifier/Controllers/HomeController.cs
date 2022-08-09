
using Microsoft.AspNetCore.Mvc;

namespace MusicClassifier.Controllers
{
    [ApiController]
    [Route("api/home")]
    public class HomeController : ControllerBase
    {
        [HttpGet]
        public string index()
        {
            return "Hello World";
        }
    }
}
