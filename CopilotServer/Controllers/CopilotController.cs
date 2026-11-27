using Microsoft.AspNetCore.Mvc;
using CopilotServer.Services;

namespace CopilotServer.Controllers;

[ApiController]
[Route("api/v1/copilot")]
public class CopilotController : ControllerBase
{
    private readonly ICopilotService _service;
    public CopilotController(ICopilotService service) => _service = service;

    [HttpPost("restart")]
    public async Task<IActionResult> Restart()
    {
        await _service.StopAsync();
        await _service.StartAsync();
        return Ok(new { status = "restart initiated" });
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        var running = System.Diagnostics.Process.GetProcessesByName("copilot").Any();
        return Ok(new { running });
    }
}
