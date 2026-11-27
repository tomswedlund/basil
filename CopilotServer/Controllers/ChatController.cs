using Microsoft.AspNetCore.Mvc;
using CopilotServer.Services;
using Microsoft.Copilot;
using System.Text.Json;
using System.Threading.Channels;

namespace CopilotServer.Controllers;

[ApiController]
[Route("api/v1/sessions/{sessionId}/chat")]
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

        var reply = await _copilot.ChatAsync(sessionId, req.Messages.Select(m => $"{m.Role}: {m.Content}").ToList());
        return Ok(new { sessionId, model = await _copilot.GetSessionModelAsync(sessionId), reply });
    }

    [HttpPost("stream")]
    public async Task Stream(string sessionId, [FromBody] ChatRequest req)
    {
        CopilotSession session;
        try
        {
            session = await _copilot.GetOrResumeSessionAsync(sessionId);
        }
        catch (KeyNotFoundException)
        {
            Response.StatusCode = 404;
            await Response.WriteAsync("Session not found");
            return;
        }

        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        var prompt = req.Messages?.LastOrDefault()?.Content ?? "";
        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        var turnEnded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var subscription = session.On<SessionEvent>(evt =>
        {
            try
            {
                var type = evt.Type ?? "";
                // The SDK may report Type as "unknown" for events it doesn't map; the real type
                // string lives in AdditionalProperties["type"].
                string? realType = type;
                if (evt.AdditionalProperties != null && evt.AdditionalProperties.TryGetValue("type", out var tp) && tp.ValueKind == JsonValueKind.String)
                    realType = tp.GetString();

                if (realType == "assistant.turn_start")
                {
                    var turnId = ExtractField(evt, "turnId") ?? "";
                    channel.Writer.TryWrite($"event: turn_start\ndata: {JsonSerializer.Serialize(new { turnId })}\n\n");
                }
                else if (realType == "assistant.message_delta" || realType == "model.message_delta")
                {
                    // Streaming delta (if the provider/runtime supports it).
                    var delta = ExtractField(evt, "deltaContent") ?? ExtractField(evt, "delta");
                    if (delta != null)
                    {
                        var payload = JsonSerializer.Serialize(new { delta });
                        channel.Writer.TryWrite($"data: {payload}\n\n");
                    }
                }
                else if (realType == "model.message")
                {
                    // The runtime sends model.message events for three things:
                    //  1. assistant with tool_calls → the LLM wants to run a tool
                    //  2. tool → the tool result fed back to the model
                    //  3. assistant with content, no tool_calls → the text response
                    var role = ExtractNestedField(evt, "message", "role") ?? "";
                    if (role == "assistant")
                    {
                        // Check for tool calls
                        var toolCallsJson = ExtractNestedFieldRaw(evt, "message", "tool_calls");
                        if (toolCallsJson != null && toolCallsJson != "null")
                        {
                            // Parse tool calls and forward each as a clean tool_call event
                            if (evt.AdditionalProperties != null && evt.AdditionalProperties.TryGetValue("data", out var data) && data.ValueKind == JsonValueKind.Object
                                && data.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.Object
                                && msg.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var tc in toolCalls.EnumerateArray())
                                {
                                    var id = tc.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String ? idProp.GetString() : "";
                                    var fnName = tc.TryGetProperty("function", out var fn) && fn.TryGetProperty("name", out var fnNameProp) && fnNameProp.ValueKind == JsonValueKind.String ? fnNameProp.GetString() : "";
                                    var fnArgs = tc.TryGetProperty("function", out var fn2) && fn2.TryGetProperty("arguments", out var fnArgsProp) && fnArgsProp.ValueKind == JsonValueKind.String ? fnArgsProp.GetString() : "{}";

                                    // Extract a concise description from the arguments
                                    var description = ExtractDescriptionFromArgs(fnName ?? "", fnArgs ?? "{}");

                                    var payload = JsonSerializer.Serialize(new
                                    {
                                        toolCallId = id,
                                        toolName = fnName,
                                        description,
                                        arguments = fnArgs
                                    });
                                    channel.Writer.TryWrite($"event: tool_call\ndata: {payload}\n\n");
                                }
                            }
                        }
                        else
                        {
                            // Normal assistant text
                            var content = ExtractNestedField(evt, "message", "content");
                            if (content != null)
                            {
                                var payload = JsonSerializer.Serialize(new { content });
                                channel.Writer.TryWrite($"event: chunk\ndata: {payload}\n\n");
                            }
                        }
                    }
                    else if (role == "tool")
                    {
                        // Tool result — extract toolCallId and content
                        var toolCallId = ExtractNestedField(evt, "message", "tool_call_id") ?? "";
                        var content = ExtractNestedField(evt, "message", "content") ?? "";
                        var payload = JsonSerializer.Serialize(new { toolCallId, content });
                        channel.Writer.TryWrite($"event: tool_result\ndata: {payload}\n\n");
                    }
                }
                else if (realType == "model.tool_execution")
                {
                    // Structured tool execution result with exit code, duration, etc.
                    if (evt.AdditionalProperties != null && evt.AdditionalProperties.TryGetValue("data", out var d) && d.ValueKind == JsonValueKind.Object)
                    {
                        var toolCallId = d.TryGetProperty("toolCallId", out var tid) && tid.ValueKind == JsonValueKind.String ? tid.GetString() : "";
                        var success = d.TryGetProperty("toolResult", out var tr) && tr.TryGetProperty("resultType", out var rt) && rt.ValueKind == JsonValueKind.String && rt.GetString() == "success";
                        var sessionLog = d.TryGetProperty("toolResult", out var tr2) && tr2.TryGetProperty("sessionLog", out var sl) && sl.ValueKind == JsonValueKind.String ? sl.GetString() : "";
                        var exitCode = d.TryGetProperty("toolResult", out var tr3) && tr3.TryGetProperty("structuredContent", out var sc) && sc.TryGetProperty("shellId", out var _) ? 
                            (d.TryGetProperty("toolResult", out var tr4) && tr4.TryGetProperty("contents", out var contents) && contents.ValueKind == JsonValueKind.Array && contents.GetArrayLength() > 0
                                && contents[0].TryGetProperty("exitCode", out var ec) && ec.ValueKind == JsonValueKind.Number ? ec.GetInt32() : 0) : 0;
                        var payload = JsonSerializer.Serialize(new { toolCallId, content = sessionLog, success, exitCode });
                        channel.Writer.TryWrite($"event: tool_execution\ndata: {payload}\n\n");
                    }
                }
                else if (realType == "assistant.turn_end")
                {
                    var turnId = ExtractField(evt, "turnId") ?? "";
                    channel.Writer.TryWrite($"event: turn_end\ndata: {JsonSerializer.Serialize(new { turnId })}\n\n");
                }
            }
            catch (Exception ex) { Console.WriteLine($"[stream] event error: {ex.Message}"); }
        });

        // Send the prompt and wait for the entire interaction (all turns) to complete.
        // We use SendAndWaitAsync here so the stream stays open until the agent is truly done.
        _ = Task.Run(async () =>
        {
            try
            {
                await _copilot.ChatSendAndWaitAsync(sessionId, prompt);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[stream] send error: {ex.Message}");
            }
            turnEnded.TrySetResult(true);
        });

        // Wait for the interaction to end (or the client to disconnect), then close the channel.
        _ = Task.Run(async () =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
            try
            {
                using var reg = linked.Token.Register(() => { turnEnded.TrySetCanceled(); channel.Writer.TryComplete(); });
                await turnEnded.Task;
            }
            catch { }
            channel.Writer.TryWrite("data: [DONE]\n\n");
            channel.Writer.TryComplete();
            subscription.Dispose();
        });

        // Drain the channel to the HTTP response.
        await foreach (var line in channel.Reader.ReadAllAsync(HttpContext.RequestAborted))
        {
            await Response.WriteAsync(line, HttpContext.RequestAborted);
            await Response.Body.FlushAsync(HttpContext.RequestAborted);
        }
    }

    private static string? ExtractField(Microsoft.Copilot.SessionEvent evt, string field)
    {
        if (evt.AdditionalProperties != null && evt.AdditionalProperties.TryGetValue("data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            if (data.TryGetProperty(field, out var val) && val.ValueKind == JsonValueKind.String)
                return val.GetString();
        }
        return null;
    }

    private static string? ExtractNestedField(Microsoft.Copilot.SessionEvent evt, string parentField, string childField)
    {
        if (evt.AdditionalProperties != null && evt.AdditionalProperties.TryGetValue("data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            if (data.TryGetProperty(parentField, out var parent) && parent.ValueKind == JsonValueKind.Object)
            {
                if (parent.TryGetProperty(childField, out var val) && val.ValueKind == JsonValueKind.String)
                    return val.GetString();
            }
        }
        return null;
    }

    private static string? ExtractNestedFieldRaw(Microsoft.Copilot.SessionEvent evt, string parentField, string childField)
    {
        if (evt.AdditionalProperties != null && evt.AdditionalProperties.TryGetValue("data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            if (data.TryGetProperty(parentField, out var parent) && parent.ValueKind == JsonValueKind.Object)
            {
                if (parent.TryGetProperty(childField, out var val))
                    return val.GetRawText();
            }
        }
        return null;
    }

    // Returns the full data.message object as JSON for forwarding to the frontend.
    private static string? ExtractModelMessageData(Microsoft.Copilot.SessionEvent evt)
    {
        if (evt.AdditionalProperties != null && evt.AdditionalProperties.TryGetValue("data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            if (data.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.Object)
                return msg.GetRawText();
        }
        return null;
    }

    // Extracts a concise human-readable description from the tool name and arguments JSON.
    private static string ExtractDescriptionFromArgs(string toolName, string argsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            var root = doc.RootElement;
            // Common fields: "description" (bash), "command" (bash), "pattern" (grep), etc.
            if (root.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.String)
                return desc.GetString() ?? toolName;
            if (root.TryGetProperty("command", out var cmd) && cmd.ValueKind == JsonValueKind.String)
                return $"{toolName}: {cmd.GetString()}";
            // Fallback: show tool name with first argument
            return toolName;
        }
        catch
        {
            return toolName;
        }
    }
}
