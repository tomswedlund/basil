using Microsoft.AspNetCore.Mvc;
using CopilotServer.Services;

namespace CopilotServer.Controllers;

[ApiController]
[Route("api/sessions/{sessionId}/chat")]
public class ChatController : ControllerBase
{
    private readonly ICopilotService _copilot;

    public ChatController(ICopilotService copilot)
    {
        _copilot = copilot;
    }

    public record ChatRequest(List<ChatMessage> Messages, string? Model = null);
    public record ChatMessage(string Role, string Content);

    [HttpPost]
    public async Task<IActionResult> Chat(string sessionId, [FromBody] ChatRequest req)
    {
        var exists = await _copilot.SessionExistsAsync(sessionId);
        if (!exists)
            return NotFound($"Session {sessionId} not found");

        var prompt = string.Join("\n", req.Messages.Select(m => $"{m.Role}: {m.Content}"));
        // Replace the following with real SDK call once the exact API is confirmed.
        // var session = _copilot.GetSession(sessionId);
        // var result = await _client.SendPromptAsync(session, prompt);
        var reply = await _copilot.ChatAsync(sessionId, req.Messages.Select(m => $"{m.Role}: {m.Content}").ToList());
        return Ok(new { sessionId, model = await _copilot.GetSessionModelAsync(sessionId), reply });
    }

    [HttpPost("stream")]
    public async Task Stream(string sessionId, [FromBody] ChatRequest req)
    {
        var exists = await _copilot.SessionExistsAsync(sessionId);
        if (!exists)
        {
            Response.StatusCode = 404;
            await Response.WriteAsync("Session not found");
            return;
        }

        Response.Headers.Add("Content-Type", "text/event-stream");
        var last = req.Messages?.LastOrDefault()?.Content ?? "";
        await Response.WriteAsync($"data: Hello from {sessionId}\n\n");
        await Response.WriteAsync($"data: You said: {last}\n\n");
        await Response.WriteAsync("data: [DONE]\n\n");
        await Response.Body.FlushAsync();
    }
}
