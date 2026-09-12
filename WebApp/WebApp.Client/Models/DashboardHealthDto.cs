namespace WebApp.Client.Models;

public sealed record DashboardHealthDto(
    DashboardHealthStatus Storage,
    DashboardHealthStatus Application,
    DashboardHealthStatus Dependencies,
    DashboardHealthStatus Backups,
    DashboardHealthStatus Overall);
