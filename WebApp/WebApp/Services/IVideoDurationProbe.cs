namespace WebApp.Services;

internal interface IVideoDurationProbe
{
    Task<TimeSpan?> GetDurationAsync(string physicalPath, CancellationToken cancellationToken);
}
