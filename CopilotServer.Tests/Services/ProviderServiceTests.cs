using CopilotServer.Services;

namespace CopilotServer.Tests.Services;

public class ProviderServiceTests
{
    private readonly ProviderService _service;

    public ProviderServiceTests()
    {
        _service = new ProviderService();
    }

    [Fact]
    public async Task ListProvidersAsync_ReturnsEmptyWhenNoConfig()
    {
        var providers = await _service.ListProvidersAsync();
        Assert.NotNull(providers);
    }

    [Fact]
    public async Task ListModelsForProviderAsync_ReturnsEmpty()
    {
        var models = await _service.ListModelsForProviderAsync("deepinfra");
        Assert.Empty(models);
    }
}
