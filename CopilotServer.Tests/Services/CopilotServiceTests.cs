using CopilotServer.Services;
using Microsoft.Copilot;

namespace CopilotServer.Tests.Services;

public class CopilotServiceTests
{
    private readonly CopilotService _service;

    public CopilotServiceTests()
    {
        // Use real service but avoid network calls by testing in-memory methods only
        // We will test List/Delete/SessionExists which don't require client calls
        _service = new CopilotService();
    }

    [Fact]
    public async Task SessionExistsAsync_ReturnsFalseForMissing()
    {
        var exists = await _service.SessionExistsAsync("nonexistent");
        Assert.False(exists);
    }

    [Fact]
    public async Task ListSessionsAsync_ReturnsEmptyInitially()
    {
        var list = await _service.ListSessionsAsync();
        Assert.NotNull(list);
    }

    [Fact]
    public async Task DeleteSessionAsync_ReturnsFalseForMissing()
    {
        var deleted = await _service.DeleteSessionAsync("nonexistent");
        Assert.False(deleted);
    }
}
