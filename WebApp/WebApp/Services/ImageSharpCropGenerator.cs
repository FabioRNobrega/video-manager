using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using WebApp.Models;

namespace WebApp.Services;

internal sealed class ImageSharpCropGenerator : IImageCropGenerator
{
    public async Task<ImageCropGenerationResult> CropAsync(
        string sourcePath, string destinationPath, int x, int y, int width, int height, CancellationToken cancellationToken)
    {
        if (width <= 0 || height <= 0)
        {
            return ImageCropGenerationResult.OutOfBounds("crop region must have a positive width and height");
        }

        var temporaryPath = BuildTemporaryPath(destinationPath);

        try
        {
            var info = await Image.IdentifyAsync(sourcePath, cancellationToken);
            if (x < 0 || y < 0 || x + width > info.Width || y + height > info.Height)
            {
                return ImageCropGenerationResult.OutOfBounds("crop region is outside the source image bounds");
            }

            using (var image = await Image.LoadAsync(sourcePath, cancellationToken))
            {
                image.Mutate(context => context.Crop(new Rectangle(x, y, width, height)));
                await image.SaveAsync(temporaryPath, cancellationToken);
            }

            if (!IsValidOutput(temporaryPath))
            {
                TryDelete(temporaryPath);
                return ImageCropGenerationResult.Failed("crop produced no readable output");
            }

            File.Move(temporaryPath, destinationPath, overwrite: false);
            return ImageCropGenerationResult.Success();
        }
        catch (OperationCanceledException)
        {
            TryDelete(temporaryPath);
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidImageContentException or UnknownImageFormatException)
        {
            TryDelete(temporaryPath);
            return ImageCropGenerationResult.Failed(exception.Message);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    internal static string BuildTemporaryPath(string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(destinationPath);
        var extension = Path.GetExtension(destinationPath);
        return Path.Combine(directory, $"{stem}.{Guid.NewGuid():N}.tmp{extension}");
    }

    private static bool IsValidOutput(string path)
    {
        try
        {
            var file = new FileInfo(path);
            return file.Exists && file.Length > 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
