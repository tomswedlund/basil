using System.Diagnostics;

namespace CopilotServer.Services;

public interface IConsoleService
{
    ProcessStartInfo CreateStartInfo(string fileName, string arguments, string? workingDirectory = null);
    Task<int> RunAsync(string fileName, string arguments, string? workingDirectory = null, CancellationToken ct = default);
    Task<bool> IsProcessRunningAsync(string processName, CancellationToken ct = default);
    Task KillProcessByNameAsync(string processName, CancellationToken ct = default);
}

public class ConsoleService : IConsoleService
{
    public ProcessStartInfo CreateStartInfo(string fileName, string arguments, string? workingDirectory = null)
    {
        return new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
    }

    public async Task<int> RunAsync(string fileName, string arguments, string? workingDirectory = null, CancellationToken ct = default)
    {
        var psi = CreateStartInfo(fileName, arguments, workingDirectory);
        using var process = new Process { StartInfo = psi };
        process.Start();
        await process.WaitForExitAsync(ct);
        return process.ExitCode;
    }

    public Task<bool> IsProcessRunningAsync(string processName, CancellationToken ct = default)
    {
        var running = Process.GetProcessesByName(processName).Length > 0;
        return Task.FromResult(running);
    }

    public async Task KillProcessByNameAsync(string processName, CancellationToken ct = default)
    {
        var processes = Process.GetProcessesByName(processName);
        foreach (var p in processes)
        {
            try { p.Kill(); await p.WaitForExitAsync(ct); }
            catch { /* ignore */ }
        }
    }
}
