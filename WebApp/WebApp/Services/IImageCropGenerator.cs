using WebApp.Models;

namespace WebApp.Services;

internal interface IImageCropGenerator
{
    Task<ImageCropGenerationResult> CropAsync(
        string sourcePath, string destinationPath, int x, int y, int width, int height, CancellationToken cancellationToken);
}
