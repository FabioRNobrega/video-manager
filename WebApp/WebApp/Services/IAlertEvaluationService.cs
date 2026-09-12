using WebApp.Client.Models;

namespace WebApp.Services;

internal readonly record struct DashboardAlertInputs(double? StoragePercent, double? MemoryPercent, double? CpuPercent);

internal interface IAlertEvaluationService
{
    IReadOnlyList<DashboardAlertDto> Evaluate(DashboardAlertInputs inputs);
}
