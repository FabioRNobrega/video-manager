using WebApp.Models;

namespace WebApp.Services;

internal interface IImageCropService
{
    Task<ImageCropOutcome> CropAsync(
        string categoryKey, string itemId, int x, int y, int width, int height, CancellationToken cancellationToken);
}
