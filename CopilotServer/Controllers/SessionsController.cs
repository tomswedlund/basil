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
        var info = await _service.GetSessionInfoAsync(id);
        if (info == null) return NotFound();
        return Ok(info);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        await _service.DeleteSessionAsync(id);
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

    [HttpPatch("{id}/title")]
    public async Task<IActionResult> UpdateTitle(string id, [FromBody] UpdateTitleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title is required");

        var exists = await _service.SessionExistsAsync(id);
        if (!exists) return NotFound();

        await _service.RenameSessionAsync(id, request.Title);
        var info = await _service.GetSessionInfoAsync(id);
        return Ok(info);
    }

    [HttpGet("{id}/messages")]
    public async Task<IActionResult> GetMessages(string id)
    {
        try
        {
            var messages = await _service.GetSessionMessagesAsync(id);
            return Ok(messages);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

}

public record CreateSessionRequest(string SessionId, string? Model);
public record UpdateModelRequest(string Model);
public record UpdateTitleRequest(string Title);
