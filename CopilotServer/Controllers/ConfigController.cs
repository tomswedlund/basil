using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;

namespace CopilotServer.Controllers;

[ApiController]
[Route("api/config")]
public class ConfigController : ControllerBase
{
    private readonly string _configPath = "/workspace/EHR/.config/copilot/config.json";
    private static readonly object _lock = new();

    [HttpGet]
    public IActionResult Get() => GetConfig();

    [HttpGet("providers")]
    public IActionResult GetProviders()
    {
        lock (_lock)
        {
            var config = LoadConfig();
            if (config.TryGetPropertyValue("inference", out var inf) && inf is JsonObject infObj &&
                infObj.TryGetPropertyValue("providers", out var prov) && prov is JsonArray arr)
            {
                var names = arr.Select(p => p?["name"]?.ToString()).Where(n => n != null).ToArray();
                return Ok(names);
            }
            return Ok(new JsonArray());
        }
    }

    [HttpPost("providers")]
    public IActionResult AddProvider([FromBody] ProviderRequest req)
    {
        lock (_lock)
        {
            var config = LoadConfig();
            var inference = GetOrCreateObject(config, "inference");
            var providers = GetOrCreateArray(config, "inference", "providers");
            
            var providerNode = new JsonObject
            {
                ["name"] = req.Name,
                ["type"] = req.Type,
                ["baseUrl"] = req.BaseUrl,
                ["wireApi"] = req.WireApi,
                ["apiKey"] = req.ApiKey
            };
            providers.Add(providerNode);
            SaveConfig(config);
            return CreatedAtAction(nameof(GetProviders), new { }, new { name = req.Name });
        }
    }

    [HttpDelete("providers/{name}")]
    public IActionResult RemoveProvider(string name)
    {
        lock (_lock)
        {
            var config = LoadConfig();
            if (config.TryGetPropertyValue("inference", out var inf) && inf is JsonObject infObj &&
                infObj.TryGetPropertyValue("providers", out var prov) && prov is JsonArray arr)
            {
                var toRemove = arr.Where(p => p?["name"]?.ToString() == name).ToList();
                foreach (var p in toRemove) arr.Remove(p);
                SaveConfig(config);
                return NoContent();
            }
            return NotFound();
        }
    }

    [HttpGet("providers/{name}")]
    public IActionResult GetProvider(string name)
    {
        lock (_lock)
        {
            var config = LoadConfig();
            if (config.TryGetPropertyValue("inference", out var inf) && inf is JsonObject infObj &&
                infObj.TryGetPropertyValue("providers", out var provNode) && provNode is JsonArray provArr)
            {
                var provider = provArr.FirstOrDefault(p => p?["name"]?.ToString() == name);
                if (provider != null) return Ok(provider);
            }
            return NotFound();
        }
    }

    [HttpPost("mcp/servers")]
    public IActionResult AddMcpServer([FromBody] McpServer server)
    {
        lock (_lock)
        {
            var config = LoadConfig();
            var servers = GetOrCreateArray(config, "mcp", "servers");
            servers.Add(JsonValue.Create(server.Name));
            SaveConfig(config);
            return Ok(new { status = "added", server = server.Name });
        }
    }

    [HttpDelete("mcp/servers/{name}")]
    public IActionResult RemoveMcpServer(string name)
    {
        lock (_lock)
        {
            var config = LoadConfig();
            if (config.TryGetPropertyValue("mcp", out var mcpNode) && mcpNode is JsonObject mcpObj)
            {
                if (mcpObj.TryGetPropertyValue("servers", out var serversNode) && serversNode is JsonArray servers)
                {
                    var toRemove = servers.Where(s => s?.ToString() == name).ToList();
                    foreach (var s in toRemove) servers.Remove(s);
                    SaveConfig(config);
                    return Ok(new { status = "removed", name });
                }
            }
            return NotFound();
        }
    }

    [HttpGet("mcp/servers")]
    public IActionResult GetMcpServers()
    {
        lock (_lock)
        {
            var config = LoadConfig();
            if (config.TryGetPropertyValue("mcp", out var mcpNode) && mcpNode is JsonObject mcpObj)
            {
                mcpObj.TryGetPropertyValue("servers", out var servers);
                return Ok(servers ?? new JsonArray());
            }
            return Ok(new JsonArray());
        }
    }

    [HttpPost("agents/default")]
    public IActionResult SetDefaultAgent([FromBody] JsonObject agent)
    {
        lock (_lock)
        {
            var config = LoadConfig();
            var agents = GetOrCreateObject(config, "agents");
            agents["default"] = agent;
            SaveConfig(config);
            return Ok(new { status = "set" });
        }
    }

    [HttpGet("agents")]
    public IActionResult GetAgents()
    {
        lock (_lock)
        {
            var config = LoadConfig();
            config.TryGetPropertyValue("agents", out var agents);
            return Ok(agents ?? new JsonObject());
        }
    }

    [HttpGet("inference/default-model")]
    public IActionResult GetDefaultModel()
    {
        lock (_lock)
        {
            var config = LoadConfig();
            var model = GetConfigValue(config, "inference.default_model", "deepinfra/glm-5.3");
            return Ok(new { default_model = model });
        }
    }

    [HttpPost("inference/default-model")]
    public IActionResult SetDefaultModel([FromBody] DefaultModelRequest req)
    {
        lock (_lock)
        {
            var config = LoadConfig();
            var inference = GetOrCreateObject(config, "inference");
            inference["default_model"] = req.Model;
            SaveConfig(config);
            return Ok(new { default_model = req.Model });
        }
    }

    private IActionResult GetConfig()
    {
        lock (_lock)
        {
            if (!System.IO.File.Exists(_configPath))
                return Ok(new JsonObject());
            var json = System.IO.File.ReadAllText(_configPath);
            var node = JsonNode.Parse(json);
            return Ok(node);
        }
    }

    private JsonObject LoadConfig()
    {
        if (!System.IO.File.Exists(_configPath))
            return new JsonObject();
        var json = System.IO.File.ReadAllText(_configPath);
        var node = JsonNode.Parse(json);
        return node as JsonObject ?? new JsonObject();
    }

    private void SaveConfig(JsonObject config)
    {
        var json = config.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        System.IO.File.WriteAllText(_configPath, json);
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

    private JsonObject GetOrCreateObject(JsonObject config, params string[] path)
    {
        JsonObject current = config;
        foreach (var p in path)
        {
            if (!current.TryGetPropertyValue(p, out var next) || next is not JsonObject obj)
            {
                obj = new JsonObject();
                current[p] = obj;
            }
            current = obj;
        }
        return current;
    }

    private JsonArray GetOrCreateArray(JsonObject config, params string[] path)
    {
        var obj = GetOrCreateObject(config, path.Take(path.Length - 1).ToArray());
        var last = path.Last();
        if (!obj.TryGetPropertyValue(last, out var arr) || arr is not JsonArray array)
        {
            array = new JsonArray();
            obj[last] = array;
        }
        return array;
    }

    public class McpServer
    {
        public string Name { get; set; } = "";
        public string Command { get; set; } = "";
        public string[] Args { get; set; } = Array.Empty<string>();
    }

    public class ProviderRequest
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string BaseUrl { get; set; } = "";
        public string WireApi { get; set; } = "chat";
        public string ApiKey { get; set; } = "";
    }

    public class DefaultModelRequest
    {
        public string Model { get; set; } = "";
    }
}
