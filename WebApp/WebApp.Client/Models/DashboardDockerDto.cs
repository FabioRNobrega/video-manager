namespace WebApp.Client.Models;

public sealed record DashboardDockerDto(
    bool IsAvailable,
    IReadOnlyList<DashboardDockerContainerDto> Containers);
