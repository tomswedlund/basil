using CopilotServer.Controllers;
using CopilotServer.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CopilotServer.Tests.Controllers;

public class CopilotControllerTests
{
    private readonly Mock<ICopilotService> _mockService;
    private readonly CopilotController _controller;

    public CopilotControllerTests()
    {
        _mockService = new Mock<ICopilotService>();
        _controller = new CopilotController(_mockService.Object);
    }

    [Fact]
    public async Task Restart_ReturnsOk()
    {
        _mockService.Setup(s => s.StopAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockService.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _controller.Restart();

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void Status_ReturnsOk()
    {
        var result = _controller.Status();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }
}
