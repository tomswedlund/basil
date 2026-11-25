using CopilotServer.Controllers;
using CopilotServer.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CopilotServer.Tests.Controllers;

public class SessionsControllerTests
{
    private readonly Mock<ICopilotService> _mockService;
    private readonly SessionsController _controller;

    public SessionsControllerTests()
    {
        _mockService = new Mock<ICopilotService>();
        _controller = new SessionsController(_mockService.Object);
    }

    [Fact]
    public async Task Create_ValidSession_ReturnsCreated()
    {
        _mockService.Setup(s => s.CreateSessionAsync("test-id", It.IsAny<string>())).ReturnsAsync("test-id");

        var result = await _controller.Create(new CreateSessionRequest("test-id", null));

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal("Get", created.ActionName);
        Assert.Equal("test-id", created.RouteValues!["id"]);
    }

    [Fact]
    public async Task Create_MissingSessionId_ReturnsBadRequest()
    {
        var result = await _controller.Create(new CreateSessionRequest("", null));
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task List_ReturnsOk()
    {
        _mockService.Setup(s => s.ListSessionsAsync()).ReturnsAsync(new List<string> { "s1", "s2" });

        var result = await _controller.List();

        var ok = Assert.IsType<OkObjectResult>(result);
        var sessions = Assert.IsAssignableFrom<List<string>>(ok.Value!);
        Assert.Equal(2, sessions.Count);
    }

    [Fact]
    public async Task Delete_ExistingSession_ReturnsNoContent()
    {
        _mockService.Setup(s => s.DeleteSessionAsync("s1")).ReturnsAsync(true);

        var result = await _controller.Delete("s1");

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_NonExistingSession_ReturnsNotFound()
    {
        _mockService.Setup(s => s.DeleteSessionAsync("missing")).ReturnsAsync(false);

        var result = await _controller.Delete("missing");

        Assert.IsType<NotFoundResult>(result);
    }
}
