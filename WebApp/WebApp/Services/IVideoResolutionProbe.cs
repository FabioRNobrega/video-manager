namespace WebApp.Services;

internal interface IVideoResolutionProbe
{
    Task<(int? Width, int? Height)> GetResolutionAsync(string physicalPath, CancellationToken cancellationToken);
}
