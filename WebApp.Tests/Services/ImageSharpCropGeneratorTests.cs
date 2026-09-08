using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class ImageSharpCropGeneratorTests
{
    [Fact]
    public async Task Crop_produces_a_file_with_the_expected_pixel_dimensions()
    {
        using var directory = new TemporaryDirectory();
        var sourcePath = Path.Combine(directory.Path, "source.png");
        await CreateFixtureImageAsync(sourcePath, 20, 10);
        var destinationPath = Path.Combine(directory.Path, "Crop 0001.png");
        var generator = new ImageSharpCropGenerator();

        var result = await generator.CropAsync(sourcePath, destinationPath, 2, 2, 8, 4, CancellationToken.None);

        Assert.Equal(ImageCropGenerationStatus.Success, result.Status);
        Assert.True(File.Exists(destinationPath));
        using var cropped = await Image.LoadAsync(destinationPath);
        Assert.Equal(8, cropped.Width);
        Assert.Equal(4, cropped.Height);
    }

    [Fact]
    public async Task Out_of_bounds_region_is_rejected_without_writing_a_file()
    {
        using var directory = new TemporaryDirectory();
        var sourcePath = Path.Combine(directory.Path, "source.png");
        await CreateFixtureImageAsync(sourcePath, 10, 10);
        var destinationPath = Path.Combine(directory.Path, "Crop 0001.png");
        var generator = new ImageSharpCropGenerator();

        var result = await generator.CropAsync(sourcePath, destinationPath, 5, 5, 10, 10, CancellationToken.None);

        Assert.Equal(ImageCropGenerationStatus.OutOfBounds, result.Status);
        Assert.False(File.Exists(destinationPath));
        Assert.DoesNotContain(Directory.EnumerateFiles(directory.Path), path => path != sourcePath);
    }

    [Fact]
    public async Task Degenerate_region_is_rejected_without_writing_a_file()
    {
        using var directory = new TemporaryDirectory();
        var sourcePath = Path.Combine(directory.Path, "source.png");
        await CreateFixtureImageAsync(sourcePath, 10, 10);
        var destinationPath = Path.Combine(directory.Path, "Crop 0001.png");
        var generator = new ImageSharpCropGenerator();

        var result = await generator.CropAsync(sourcePath, destinationPath, 0, 0, 0, 5, CancellationToken.None);

        Assert.Equal(ImageCropGenerationStatus.OutOfBounds, result.Status);
        Assert.False(File.Exists(destinationPath));
    }

    [Fact]
    public void Temporary_path_is_unique_and_derived_from_destination()
    {
        const string destination = "/pictures/cuts/Beach Sunset 0001.jpg";

        var first = ImageSharpCropGenerator.BuildTemporaryPath(destination);
        var second = ImageSharpCropGenerator.BuildTemporaryPath(destination);

        Assert.NotEqual(first, second);
        Assert.StartsWith("/pictures/cuts/Beach Sunset 0001.", first);
        Assert.EndsWith(".tmp.jpg", first);
    }

    private static async Task CreateFixtureImageAsync(string path, int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        await image.SaveAsPngAsync(path);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"video-manager-image-crop-generator-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
