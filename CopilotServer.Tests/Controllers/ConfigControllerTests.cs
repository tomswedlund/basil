using CopilotServer.Controllers;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;

namespace CopilotServer.Tests.Controllers;

public class ConfigControllerTests
{
    private readonly ConfigController _controller;

    public ConfigControllerTests()
    {
        _controller = new ConfigController();
    }

    [Fact]
    public void Get_ReturnsOk()
    {
        var result = _controller.Get();
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public void AddMcpServer_ReturnsOk()
    {
        var server = new ConfigController.McpServer { Name = "test", Command = "npx" };
        var result = _controller.AddMcpServer(server);
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public void AddProvider_ReturnsCreated()
    {
        var provider = new ConfigController.ProviderRequest 
        { 
            Name = "deepinfra", 
            Type = "openai",
            BaseUrl = "https://api.deepinfra.com/v1/openai"
        };
        var result = _controller.AddProvider(provider);
        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.NotNull(created.Value);
    }

    [Fact]
    public void GetProvider_ReturnsOkOrNotFound()
    {
        var result = _controller.GetProvider("deepinfra");
        Assert.True(result is OkObjectResult || result is NotFoundResult);
    }

    [Fact]
    public void GetMcpServers_ReturnsOk()
    {
        var result = _controller.GetMcpServers();
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public void SetDefaultAgent_ReturnsOk()
    {
        var agent = new JsonObject { ["name"] = "coder" };
        var result = _controller.SetDefaultAgent(agent);
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public void GetAgents_ReturnsOk()
    {
        var result = _controller.GetAgents();
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public void GetDefaultModel_ReturnsOk()
    {
        var result = _controller.GetDefaultModel();
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public void SetDefaultModel_ReturnsOk()
    {
        var req = new ConfigController.DefaultModelRequest { Model = "deepinfra/glm-5.3" };
        var result = _controller.SetDefaultModel(req);
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }
}
