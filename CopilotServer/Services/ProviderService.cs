using System.Text.Json;
using Microsoft.Copilot;

namespace CopilotServer.Services;

public interface IProviderService
{
    Task<IReadOnlyList<string>> ListProvidersAsync();
    Task<IReadOnlyList<string>> ListModelsForProviderAsync(string provider);
    Task CreateProviderAsync(string name, string type, string baseUrl, string apiKey);
    Task DeleteProviderAsync(string name);
    Task AddModelAsync(string provider, string model);
    Task RemoveModelAsync(string provider, string model);
}

public class ProviderService : IProviderService
{
    private readonly CopilotClient _client;
    private readonly string _configPath = "/workspace/EHR/.config/copilot/config.json";

    public ProviderService()
    {
        _client = null!;
    }

    public ProviderService(CopilotClient client)
    {
        _client = client;
    }
    public ProviderService(CopilotServer.Services.CopilotService service)
    {
        _client = service.Client;
    }

    private ConfigFile LoadConfig()
    {
        if (!File.Exists(_configPath)) return new ConfigFile();
        var json = File.ReadAllText(_configPath);
        return JsonSerializer.Deserialize<ConfigFile>(json) ?? new ConfigFile();
    }

    private void SaveConfig(ConfigFile cfg)
    {
        var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configPath, json);
    }

    public Task<IReadOnlyList<string>> ListProvidersAsync()
    {
        var cfg = LoadConfig();
        var providers = cfg.Inference?.Providers?.Select(p => p.Name).ToList() ?? new List<string>();
        return Task.FromResult<IReadOnlyList<string>>(providers);
    }

    public Task<IReadOnlyList<string>> ListModelsForProviderAsync(string provider)
    {
        // Models are discovered by the provider at runtime; return empty for now.
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    public Task CreateProviderAsync(string name, string type, string baseUrl, string apiKey)
    {
        var cfg = LoadConfig();
        cfg.Inference ??= new InferenceConfig();
        cfg.Inference.Providers ??= new List<ProviderConfig>();
        if (!cfg.Inference.Providers.Any(p => p.Name == name))
        {
            cfg.Inference.Providers.Add(new ProviderConfig
            {
                Name = name,
                Type = type,
                BaseUrl = baseUrl,
                WireApi = "chat",
                ApiKey = apiKey
            });
            if (string.IsNullOrEmpty(cfg.Inference.DefaultProvider))
                cfg.Inference.DefaultProvider = name;
        }
        SaveConfig(cfg);
        return Task.CompletedTask;
    }

    public Task DeleteProviderAsync(string name)
    {
        var cfg = LoadConfig();
        cfg.Inference?.Providers?.RemoveAll(p => p.Name == name);
        SaveConfig(cfg);
        return Task.CompletedTask;
    }

    public Task AddModelAsync(string provider, string model)
    {
        // Models are provider-native; no config change needed. No-op for direct SDK.
        return Task.CompletedTask;
    }

    public Task RemoveModelAsync(string provider, string model)
    {
        return Task.CompletedTask;
    }

    private class ConfigFile
    {
        public GithubConfig? Github { get; set; }
        public InferenceConfig? Inference { get; set; }
    }
    private class GithubConfig { public CapiConfig? Capi { get; set; } }
    private class CapiConfig { public bool Enabled { get; set; } }
    private class InferenceConfig
    {
        public string? DefaultProvider { get; set; }
        public List<ProviderConfig>? Providers { get; set; }
    }
    private class ProviderConfig
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? BaseUrl { get; set; }
        public string? WireApi { get; set; }
        public string? ApiKey { get; set; }
    }
}
