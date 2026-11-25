using CopilotServer.Services;
using System.Diagnostics;

namespace CopilotServer.Tests.Services;

public class ConsoleServiceTests
{
    private readonly ConsoleService _service = new();

    [Fact]
    public void CreateStartInfo_SetsPropertiesCorrectly()
    {
        var info = _service.CreateStartInfo("myapp", "arg1 arg2", "/tmp");
        Assert.Equal("myapp", info.FileName);
        Assert.Equal("arg1 arg2", info.Arguments);
        Assert.Equal("/tmp", info.WorkingDirectory);
        Assert.False(info.UseShellExecute);
        Assert.True(info.RedirectStandardOutput);
    }

    [Fact]
    public async Task IsProcessRunningAsync_ReturnsFalseForUnknown()
    {
        var running = await _service.IsProcessRunningAsync("definitely_not_running_12345");
        Assert.False(running);
    }

    [Fact]
    public async Task RunAsync_ExecutesEcho()
    {
        var code = await _service.RunAsync("echo", "hello");
        Assert.Equal(0, code);
    }
}
