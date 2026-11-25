using CopilotServer.Controllers;
using CopilotServer.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CopilotServer.Tests.Controllers;

public class ChatControllerTests
{
    private readonly Mock<ICopilotService> _mockService;
    private readonly ChatController _controller;

    public ChatControllerTests()
    {
        _mockService = new Mock<ICopilotService>();
        _controller = new ChatController(_mockService.Object);
    }

    [Fact]
    public async Task Chat_ExistingSession_ReturnsOk()
    {
        _mockService.Setup(s => s.SessionExistsAsync("s1")).ReturnsAsync(true);
        _mockService.Setup(s => s.GetSessionModelAsync("s1")).ReturnsAsync("deepinfra/glm-5.3");
        _mockService.Setup(s => s.ChatAsync("s1", It.IsAny<List<string>>())).ReturnsAsync("Hello");

        var req = new ChatController.ChatRequest(new List<ChatController.ChatMessage> { new("user","hi") });

        var result = await _controller.Chat("s1", req);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task Chat_MissingSession_ReturnsNotFound()
    {
        _mockService.Setup(s => s.SessionExistsAsync("missing")).ReturnsAsync(false);

        var req = new ChatController.ChatRequest(new List<ChatController.ChatMessage> { new("user","hi") });

        var result = await _controller.Chat("missing", req);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
