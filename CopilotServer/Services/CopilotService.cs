using Microsoft.Copilot;
using System.Diagnostics;
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
    Task<IReadOnlyList<string>> ListSessionsAsync();
    Task<bool> SessionExistsAsync(string sessionId);
    Task<bool> UpdateSessionModelAsync(string sessionId, string newModel);
    Task<string?> GetSessionModelAsync(string sessionId);
    CopilotSession? GetSession(string sessionId);
    Task<string> ChatAsync(string sessionId, List<string> messages);
}

public class CopilotService : ICopilotService
{
    private readonly CopilotClient _client;
    private readonly IConsoleService _console;
    public CopilotClient Client => _client;
    private readonly Dictionary<string, CopilotSession> _sessions = new();
    private readonly Dictionary<string, string> _sessionModels = new();

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
            var psi = _console.CreateStartInfo(file, args, "/workspace/EHR");
            using var proc = Process.Start(psi);
            await Task.Delay(2000, ct);
        }
    }

    public Task StartAsync(CancellationToken ct = default) => _client.StartAsync();
    public Task StopAsync(CancellationToken ct = default) => _client.StopAsync();

    private readonly string _configPath = "/workspace/EHR/.config/copilot/config.json";
    
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
            GitHub = new SessionGitHubOptions { Capi = new CapiSessionOptions { Enabled = false } },
            Inference = new SessionInferenceOptions
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
            }
        });
        lock (_sessions)
        {
            _sessions[sessionId] = session;
            _sessionModels[sessionId] = model;
        }
        return sessionId;
    }

    public Task<bool> DeleteSessionAsync(string sessionId)
    {
        lock (_sessions) { return Task.FromResult(_sessions.Remove(sessionId)); }
    }

    public Task<IReadOnlyList<string>> ListSessionsAsync()
    {
        lock (_sessions) { return Task.FromResult((IReadOnlyList<string>)_sessions.Keys.ToList()); }
    }

    public Task<bool> SessionExistsAsync(string sessionId)
    {
        lock (_sessions) { return Task.FromResult(_sessions.ContainsKey(sessionId)); }
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

    public async Task<string> ChatAsync(string sessionId, List<string> messages)
    {
        var session = GetSession(sessionId);
        if (session == null) throw new KeyNotFoundException($"Session {sessionId} not found");
        var prompt = string.Join("\n", messages);
        var response = await session.SendAndWaitAsync(new MessageOptions { Prompt = prompt });
        return response?.Data?.Content ?? string.Empty;
    }
}
