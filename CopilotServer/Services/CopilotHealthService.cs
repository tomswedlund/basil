using Microsoft.Extensions.Hosting;
using System.Diagnostics;

namespace CopilotServer.Services;

public interface ICopilotHealthService
{
    Task EnsureHealthyAsync(CancellationToken ct = default);
}

public class CopilotHealthService : BackgroundService, ICopilotHealthService
{
    private readonly IConsoleService _console;
    private readonly ICopilotService _copilot;
    private readonly ILogger<CopilotHealthService> _logger;
    private readonly string _processName = "copilot";
    private readonly string _executable = "/workspace/EHR/copilot-bin/copilot";
    private readonly string _args = "--headless --port 4321";
    private readonly string _workingDir = "/workspace/EHR";

    public CopilotHealthService(IConsoleService console, ICopilotService copilot, ILogger<CopilotHealthService> logger)
    {
        _console = console;
        _copilot = copilot;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Copilot health monitor started");
        await EnsureCopilotRunningAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                await EnsureCopilotRunningAsync(stoppingToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Copilot health check");
            }
        }
    }

    public async Task EnsureHealthyAsync(CancellationToken ct = default)
    {
        await EnsureCopilotRunningAsync(ct);
    }

    private async Task EnsureCopilotRunningAsync(CancellationToken ct)
    {
        var running = await _console.IsProcessRunningAsync(_processName, ct);
        if (!running)
        {
            _logger.LogWarning("Copilot process not running, starting it...");
            var psi = _console.CreateStartInfo(_executable, _args, _workingDir);
            using var proc = Process.Start(psi);
            await Task.Delay(2000, ct);
            _logger.LogInformation("Copilot process started");
        }
        else
        {
            _logger.LogDebug("Copilot process is healthy");
        }
    }
}
