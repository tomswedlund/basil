using Microsoft.Copilot;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
#pragma warning disable MCPSDK001

namespace CopilotServer.Services;

public interface ICopilotService
{
    CopilotClient Client { get; }
    Task EnsureCopilotRunningAsync(CancellationToken ct = default);
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    Task<string> CreateSessionAsync(string sessionId, string model = "deepinfra/glm-5.3");
    Task<bool> DeleteSessionAsync(string sessionId);
    Task<IReadOnlyList<SessionInfo>> ListSessionsAsync();
    Task<SessionInfo?> GetSessionInfoAsync(string sessionId);
    Task<bool> SessionExistsAsync(string sessionId);
    Task<bool> UpdateSessionModelAsync(string sessionId, string newModel);
    Task<bool> RenameSessionAsync(string sessionId, string newTitle);
    Task<string?> GetSessionModelAsync(string sessionId);
    CopilotSession? GetSession(string sessionId);
    Task<CopilotSession> GetOrResumeSessionAsync(string sessionId);
    Task<string> ChatAsync(string sessionId, List<string> messages);
    Task ChatSendAsync(string sessionId, string prompt);
    Task ChatSendAndWaitAsync(string sessionId, string prompt);
    Task<List<ChatMessageDto>> GetSessionMessagesAsync(string sessionId);
}

public record ChatMessageDto(string Role, string Content, string? ToolCallId, string? ToolName, string? Description, string? Arguments, string? Status, string? Result);

public record SessionInfo(string Id, string Title, DateTimeOffset ModifiedTime);

public class CopilotService : ICopilotService
{
    private readonly CopilotClient _client;
    private readonly IConsoleService _console;
    public CopilotClient Client => _client;
    private readonly Dictionary<string, CopilotSession> _sessions = new();
    private readonly Dictionary<string, string> _sessionModels = new();
    private readonly Dictionary<string, string> _sessionTitles = new();

    public CopilotService()
    {
        var options = new CopilotClientOptions
        {
            Mode = CopilotClientMode.CodingAgent,
            Runtime = RuntimeConnection.ForTcp("localhost", 4321)
        };
        _client = new CopilotClient(options);
        _console = new ConsoleService();
    }

    public CopilotService(CopilotClient client, IConsoleService consoleService)
    {
        _client = client;
        _console = consoleService;
    }

    public async Task EnsureCopilotRunningAsync(CancellationToken ct = default)
    {
        var isRunning = await _console.IsProcessRunningAsync("copilot", ct);
        if (!isRunning)
        {
            var file = "/workspace/EHR/copilot-bin/copilot";
            var args = "--headless --port 4321";
            var psi = _console.CreateStartInfo(file, args, "/workspace/Basil");
            using var proc = Process.Start(psi);
            await Task.Delay(2000, ct);
        }
    }

    public Task StartAsync(CancellationToken ct = default) => _client.StartAsync();
    public Task StopAsync(CancellationToken ct = default) => _client.StopAsync();

    private readonly string _configPath = "/workspace/Basil/.config/copilot/config.json";
    
    private (string provider, string modelId, string wireModel) ResolveModel(string model)
    {
        if (!string.IsNullOrWhiteSpace(model) && model.Contains("/"))
        {
            var parts = model.Split('/', 2);
            var provider = parts[0];
            var modelId = parts[1];
            var wireModel = model; // Assume wireModel matches
            if (provider == "deepinfra" && modelId == "glm-5.3")
                wireModel = "zai-org/GLM-5.3";
            return (provider, modelId, wireModel);
        }
        
        // Load from config - custom default_model field
        var config = LoadConfig();
        var defaultProvider = GetConfigValue(config, "inference.default_provider", "deepinfra");
        var defaultModel = GetConfigValue(config, "inference.default_model", "deepinfra/glm-5.3");
        
        var parts2 = defaultModel.Split('/', 2);
        var modelId2 = parts2.Length > 1 ? parts2[1] : "glm-5.3";
        var provider2 = parts2.Length > 1 ? parts2[0] : defaultProvider;
        var wireModel2 = defaultModel;
        if (provider2 == "deepinfra" && modelId2 == "glm-5.3")
            wireModel2 = "zai-org/GLM-5.3";
        
        return (provider2, modelId2, wireModel2);
    }

    private string GetProviderBaseUrl(string provider)
    {
        var config = LoadConfig();
        if (config.TryGetPropertyValue("inference", out var inf) && inf is JsonObject infObj)
        {
            if (infObj.TryGetPropertyValue("providers", out var provNode) && provNode is JsonArray provArr)
            {
                var prov = provArr.FirstOrDefault(p => p?["name"]?.ToString() == provider);
                if (prov != null) return prov["baseUrl"]?.ToString() ?? "";
            }
        }
        return provider == "deepinfra" ? "https://api.deepinfra.com/v1/openai" : "";
    }

    private string GetProviderApiKey(string provider)
    {
        return provider == "deepinfra" ? "sk-di-sMmEbw9M1LfAAPGqEcIzo7xnXoscOhwO" : "";
    }

    private JsonObject LoadConfig()
    {
        if (!System.IO.File.Exists(_configPath))
            return new JsonObject();
        var json = System.IO.File.ReadAllText(_configPath);
        var node = JsonNode.Parse(json);
        return node as JsonObject ?? new JsonObject();
    }

    private string GetConfigValue(JsonObject config, string path, string defaultValue)
    {
        var parts = path.Split('.');
        JsonNode? current = config;
        foreach (var p in parts)
        {
            if (current is JsonObject obj && obj.TryGetPropertyValue(p, out var next))
                current = next;
            else return defaultValue;
        }
        return current?.ToString() ?? defaultValue;
    }

    public async Task<string> CreateSessionAsync(string sessionId, string model = "")
    {
        var (providerName, modelId, wireModel) = ResolveModel(model);
        
        var session = await _client.CreateSessionAsync(new CreateSessionOptions
        {
            Model = $"{providerName}/{modelId}",
            SessionId = sessionId,
            WorkingDirectory = "/workspace/Basil",
            GitHub = new SessionGitHubOptions { Capi = new CapiSessionOptions { Enabled = false } },
            Inference = BuildInferenceOptions(providerName, modelId, wireModel)
        });
        lock (_sessions)
        {
            _sessions[sessionId] = session;
            _sessionModels[sessionId] = model;
        }
        return sessionId;
    }

    public async Task<bool> DeleteSessionAsync(string sessionId)
    {
        lock (_sessions) { _sessions.Remove(sessionId); }
        lock (_sessionModels) { _sessionModels.Remove(sessionId); }
        lock (_sessionTitles) { _sessionTitles.Remove(sessionId); }
        await _client.DeleteSessionAsync(sessionId);
        return true;
    }

    // Title override takes precedence over the runtime's Summary.
    private string ResolveTitle(string sessionId, string runtimeSummary)
    {
        lock (_sessionTitles)
        {
            return _sessionTitles.TryGetValue(sessionId, out var t) && !string.IsNullOrWhiteSpace(t)
                ? t
                : (string.IsNullOrWhiteSpace(runtimeSummary) ? sessionId : runtimeSummary);
        }
    }

    public async Task<IReadOnlyList<SessionInfo>> ListSessionsAsync()
    {
        var metas = await _client.ListSessionsAsync(null);
        var infos = metas
            .Select(m => new SessionInfo(
                m.SessionId,
                ResolveTitle(m.SessionId, m.Summary),
                m.ModifiedTime))
            .OrderByDescending(m => m.ModifiedTime)
            .ToList();
        return infos;
    }

    public async Task<SessionInfo?> GetSessionInfoAsync(string sessionId)
    {
        var meta = await _client.GetSessionMetadataAsync(sessionId);
        if (meta == null) return null;
        return new SessionInfo(
            meta.SessionId,
            ResolveTitle(meta.SessionId, meta.Summary),
            meta.ModifiedTime);
    }

    public async Task<bool> SessionExistsAsync(string sessionId)
    {
        // Prefer an in-memory handle (covers a freshly created session that the runtime
        // does not yet list, since it only surfaces non-empty sessions).
        if (GetSession(sessionId) != null) return true;
        var info = await GetSessionInfoAsync(sessionId);
        return info != null;
    }

    public Task<bool> UpdateSessionModelAsync(string sessionId, string newModel)
    {
        lock (_sessions)
        {
            if (!_sessions.ContainsKey(sessionId)) return Task.FromResult(false);
            _sessionModels[sessionId] = newModel;
        }
        return Task.FromResult(true);
    }

    public Task<bool> RenameSessionAsync(string sessionId, string newTitle)
    {
        if (string.IsNullOrWhiteSpace(newTitle)) return Task.FromResult(false);
        lock (_sessionTitles) { _sessionTitles[sessionId] = newTitle.Trim(); }
        return Task.FromResult(true);
    }

    public Task<string?> GetSessionModelAsync(string sessionId)
    {
        lock (_sessionModels)
        {
            _sessionModels.TryGetValue(sessionId, out var m);
            return Task.FromResult(m);
        }
    }

    public CopilotSession? GetSession(string sessionId)
    {
        lock (_sessions) { _sessions.TryGetValue(sessionId, out var s); return s; }
    }

    public async Task<CopilotSession> GetOrResumeSessionAsync(string sessionId)
    {
        var existing = GetSession(sessionId);
        if (existing != null) return existing;
        var meta = await _client.GetSessionMetadataAsync(sessionId);
        if (meta == null) throw new KeyNotFoundException($"Session {sessionId} not found");
        var (providerName, modelId, wireModel) = ResolveModel(_sessionModels.GetValueOrDefault(sessionId) ?? "");
        var session = await _client.ResumeSessionAsync(sessionId, new ResumeSessionOptions
        {
            WorkingDirectory = "/workspace/Basil",
            Inference = BuildInferenceOptions(providerName, modelId, wireModel)
        });
        lock (_sessions) { _sessions[sessionId] = session; }
        return session;
    }

    private SessionInferenceOptions BuildInferenceOptions(string providerName, string modelId, string wireModel)
    {
        return new SessionInferenceOptions
        {
            DefaultProvider = providerName,
            Providers = new[]
            {
                new NamedProviderConfig
                {
                    Name = providerName,
                    Type = "openai",
                    BaseUrl = GetProviderBaseUrl(providerName),
                    WireApi = "chat",
                    ApiKey = GetProviderApiKey(providerName)
                }
            },
            Models = new[]
            {
                new ProviderModelConfig
                {
                    Id = modelId,
                    Provider = providerName,
                    WireModel = wireModel
                }
            }
        };
    }

    public async Task<string> ChatAsync(string sessionId, List<string> messages)
    {
        var session = await GetOrResumeSessionAsync(sessionId);
        // The Copilot session maintains its own transcript, so send only the latest user prompt.
        var prompt = messages.LastOrDefault() ?? "";
        var response = await session.SendAndWaitAsync(new MessageOptions { Prompt = prompt });
        return response?.Data?.Content ?? string.Empty;
    }

    public async Task ChatSendAsync(string sessionId, string prompt)
    {
        var session = await GetOrResumeSessionAsync(sessionId);
        // Non-blocking send: returns once the turn is accepted; events stream via On(...).
        await session.SendAsync(new MessageOptions { Prompt = prompt });
    }

    public async Task ChatSendAndWaitAsync(string sessionId, string prompt)
    {
        var session = await GetOrResumeSessionAsync(sessionId);
        // Blocking send: waits for the entire interaction (all turns) to complete.
        await session.SendAndWaitAsync(new MessageOptions { Prompt = prompt });
    }

    public async Task<List<ChatMessageDto>> GetSessionMessagesAsync(string sessionId)
    {
        var session = await GetOrResumeSessionAsync(sessionId);
        var rawEvents = await session.GetRawEventsAsync();
        var messages = new List<ChatMessageDto>();
        // Track pending tool calls by toolCallId so we can attach results when they complete.
        var toolCallIndex = new Dictionary<string, int>();

        foreach (var raw in rawEvents)
        {
            if (raw.ValueKind != JsonValueKind.Object) continue;
            if (!raw.TryGetProperty("type", out var typeProp) || typeProp.ValueKind != JsonValueKind.String) continue;
            var type = typeProp.GetString() ?? "";
            if (!raw.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object) continue;

            // User messages
            if (type == "user.message")
            {
                if (data.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                {
                    var content = c.GetString() ?? "";
                    if (!string.IsNullOrWhiteSpace(content))
                        messages.Add(new ChatMessageDto("user", content, null, null, null, null, null, null));
                }
            }
            // Assistant messages — may contain text, tool requests, or both
            else if (type == "assistant.message")
            {
                var content = data.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
                    ? c.GetString() ?? ""
                    : "";

                // Check for tool requests
                if (data.TryGetProperty("toolRequests", out var tr) && tr.ValueKind == JsonValueKind.Array && tr.GetArrayLength() > 0)
                {
                    foreach (var req in tr.EnumerateArray())
                    {
                        var name = req.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? "" : "";
                        var tcId = req.TryGetProperty("toolCallId", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() ?? "" : "";
                        var argsObj = req.TryGetProperty("arguments", out var a) ? a : default;
                        var argsJson = argsObj.ValueKind == JsonValueKind.Object ? argsObj.GetRawText() : "{}";
                        var intention = req.TryGetProperty("intentionSummary", out var isum) && isum.ValueKind == JsonValueKind.String ? isum.GetString() : "";
                        var toolTitle = req.TryGetProperty("toolTitle", out var tt) && tt.ValueKind == JsonValueKind.String ? tt.GetString() : "";
                        var description = !string.IsNullOrWhiteSpace(intention) ? intention
                                         : !string.IsNullOrWhiteSpace(toolTitle) ? toolTitle
                                         : name;

                        var msg = new ChatMessageDto("tool_call", "", tcId, name, description, argsJson, "running", null);
                        messages.Add(msg);
                        toolCallIndex[tcId] = messages.Count - 1;
                    }
                }

                // Also add any text content
                if (!string.IsNullOrWhiteSpace(content))
                    messages.Add(new ChatMessageDto("assistant", content, null, null, null, null, null, null));
            }
            // Tool execution results — pair with the tool call
            else if (type == "tool.execution_complete")
            {
                var content = "";
                if (data.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Object)
                {
                    if (result.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                        content = c.GetString() ?? "";
                }
                var toolCallId = data.TryGetProperty("toolCallId", out var tid) && tid.ValueKind == JsonValueKind.String
                    ? tid.GetString() ?? ""
                    : "";

                if (toolCallIndex.TryGetValue(toolCallId, out var idx))
                {
                    var orig = messages[idx];
                    messages[idx] = orig with { Status = "complete", Result = content };
                    toolCallIndex.Remove(toolCallId);
                }
            }
        }
        return messages;
    }
}
