using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class DockerMetricsServiceTests
{
    [Fact]
    public async Task Maps_container_samples_into_dashboard_dto()
    {
        var service = new DockerMetricsService(new FakeDockerApiClient(
            [new DockerContainerSample("webapp", 12.5, 1024, 3600, 2)]));

        var result = await service.GetDockerMetricsAsync(CancellationToken.None);

        Assert.True(result.IsAvailable);
        var container = Assert.Single(result.Containers);
        Assert.Equal("webapp", container.Name);
        Assert.Equal(12.5, container.CpuPercent);
        Assert.Equal(1024, container.MemoryUsedBytes);
        Assert.Equal(3600, container.UptimeSeconds);
        Assert.Equal(2, container.RestartCount);
    }

    [Fact]
    public async Task Unreachable_socket_returns_unavailable_instead_of_throwing()
    {
        var service = new DockerMetricsService(new ThrowingDockerApiClient());

        var result = await service.GetDockerMetricsAsync(CancellationToken.None);

        Assert.False(result.IsAvailable);
        Assert.Empty(result.Containers);
    }

    private sealed class FakeDockerApiClient(IReadOnlyList<DockerContainerSample> samples) : IDockerApiClient
    {
        public Task<IReadOnlyList<DockerContainerSample>> ListContainersAsync(CancellationToken cancellationToken) =>
            Task.FromResult(samples);
    }

    private sealed class ThrowingDockerApiClient : IDockerApiClient
    {
        public Task<IReadOnlyList<DockerContainerSample>> ListContainersAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("socket unreachable");
    }
}
