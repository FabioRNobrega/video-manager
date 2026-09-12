namespace WebApp.Client.Models;

public sealed record DashboardHistoryDto(IReadOnlyList<DashboardHistorySampleDto> Samples);
