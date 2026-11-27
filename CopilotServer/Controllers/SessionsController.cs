using Microsoft.AspNetCore.Mvc;
using CopilotServer.Services;

namespace CopilotServer.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class SessionsController : ControllerBase
{
    private readonly ICopilotService _service;

    public SessionsController(ICopilotService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSessionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
            return BadRequest("SessionId is required");

        try
        {
            var id = await _service.CreateSessionAsync(request.SessionId, request.Model ?? "deepinfra/glm-5.3");
            return CreatedAtAction(nameof(Get), new { id }, new { id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var sessions = await _service.ListSessionsAsync();
        return Ok(sessions);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var exists = await _service.SessionExistsAsync(id);
        if (!exists) return NotFound();
        return Ok(new { id });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await _service.DeleteSessionAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }

    [HttpPatch("{id}/model")]
    public async Task<IActionResult> UpdateModel(string id, [FromBody] UpdateModelRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
            return BadRequest("Model is required");
        
        var updated = await _service.UpdateSessionModelAsync(id, request.Model);
        if (!updated) return NotFound();
        
        return Ok(new { id, model = request.Model });
    }

}

public record CreateSessionRequest(string SessionId, string? Model);
public record UpdateModelRequest(string Model);
