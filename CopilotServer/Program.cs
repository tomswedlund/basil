using CopilotServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<ICopilotService, CopilotService>();
builder.Services.AddSingleton<IProviderService, ProviderService>(sp => new ProviderService(sp.GetRequiredService<ICopilotService>().Client));
builder.Services.AddSingleton<IConsoleService, ConsoleService>();
builder.Services.AddHostedService<CopilotHealthService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var service = scope.ServiceProvider.GetRequiredService<ICopilotService>();
    await service.EnsureCopilotRunningAsync();
    await service.StartAsync();
}

app.MapControllers();

app.MapGet("/", () => "Copilot Server API");

var lifetime = app.Lifetime;
lifetime.ApplicationStopping.Register(async () =>
{
    using var scope = app.Services.CreateScope();
    var service = scope.ServiceProvider.GetRequiredService<ICopilotService>();
    await service.StopAsync();
});

app.Run();
