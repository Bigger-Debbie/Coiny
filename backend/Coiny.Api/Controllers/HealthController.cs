using Microsoft.AspNetCore.Mvc;

namespace Coiny.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok("Coiny API is running");
        }
    }
}
