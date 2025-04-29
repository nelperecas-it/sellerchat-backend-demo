using Microsoft.AspNetCore.Mvc;
using SCIABackendDemo.Services;

namespace SCIABackendDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PromptController : ControllerBase
    {
        private readonly PromptService _promptService;

        public PromptController(PromptService promptService)
        {
            _promptService = promptService;
        }

        // GET api/prompt
        [HttpGet]
        public IActionResult GetPrompt()
        {
            return Ok(new { systemPrompt = _promptService.CurrentPrompt });
        }

        // POST api/prompt
        // Body: { "systemPrompt": "Nuevo prompt..." }
        [HttpPost]
        public IActionResult SetPrompt([FromBody] SystemPromptRequest req)
        {
            _promptService.SetPrompt(req.SystemPrompt);
            return NoContent();
        }
    }

    public class SystemPromptRequest
    {
        public string SystemPrompt { get; set; } = null!;
    }
}
