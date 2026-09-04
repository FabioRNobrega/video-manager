using WebApp.Client.Pages;
using WebApp.Components;
using WebApp.Configuration;
using WebApp.Endpoints;
using WebApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddOptions<VideoLibraryOptions>()
    .Bind(builder.Configuration.GetSection(VideoLibraryOptions.SectionName))
    .Validate(VideoLibraryOptions.HasConfiguredPath, "VideoLibrary:Path is required.")
    .Validate(VideoLibraryOptions.HasAbsolutePath, "VideoLibrary:Path must be absolute.")
    .Validate(VideoLibraryOptions.DirectoryExists, "VideoLibrary:Path must identify an existing directory.")
    .Validate(VideoLibraryOptions.DirectoryIsReadable, "VideoLibrary:Path must identify a readable directory.")
    .ValidateOnStart();
builder.Services.AddOptions<ThumbnailCacheOptions>()
    .Bind(builder.Configuration.GetSection(ThumbnailCacheOptions.SectionName))
    .Validate(ThumbnailCacheOptions.HasConfiguredPath, "ThumbnailCache:Path is required.")
    .Validate(ThumbnailCacheOptions.HasAbsolutePath, "ThumbnailCache:Path must be absolute.")
    .Validate(ThumbnailCacheOptions.DirectoryExists, "ThumbnailCache:Path must identify an existing directory.")
    .Validate(ThumbnailCacheOptions.DirectoryIsWritable, "ThumbnailCache:Path must identify a writable directory.")
    .Validate(
        options => ThumbnailCacheOptions.IsDisjointFromVideoRoot(
            options, builder.Configuration[$"{VideoLibraryOptions.SectionName}:Path"]),
        "ThumbnailCache:Path must not overlap VideoLibrary:Path.")
    .ValidateOnStart();
builder.Services.AddOptions<VideoCutOptions>()
    .Bind(builder.Configuration.GetSection(VideoCutOptions.SectionName))
    .Validate(VideoCutOptions.HasConfiguredPath, "VideoCut:Path is required.")
    .Validate(VideoCutOptions.HasAbsolutePath, "VideoCut:Path must be absolute.")
    .Validate(VideoCutOptions.DirectoryExists, "VideoCut:Path must identify an existing directory.")
    .Validate(VideoCutOptions.DirectoryIsWritable, "VideoCut:Path must identify a writable directory.")
    .Validate(VideoCutOptions.HasPositiveQueueCapacity, "VideoCut:QueueCapacity must be greater than zero.")
    .ValidateOnStart();
builder.Services.AddOptions<VideoCompositionOptions>()
    .Bind(builder.Configuration.GetSection(VideoCompositionOptions.SectionName))
    .Validate(VideoCompositionOptions.HasConfiguredPath, "VideoComposition:Path is required.")
    .Validate(VideoCompositionOptions.HasAbsolutePath, "VideoComposition:Path must be absolute.")
    .Validate(VideoCompositionOptions.DirectoryExists, "VideoComposition:Path must identify an existing directory.")
    .Validate(VideoCompositionOptions.DirectoryIsWritable, "VideoComposition:Path must identify a writable directory.")
    .Validate(VideoCompositionOptions.HasPositiveQueueCapacity, "VideoComposition:QueueCapacity must be greater than zero.")
    .Validate(VideoCompositionOptions.HasPositiveTransitionDuration, "VideoComposition:TransitionDurationSeconds must be greater than zero.")
    .ValidateOnStart();
builder.Services.AddOptions<HoverPreviewOptions>()
    .Bind(builder.Configuration.GetSection(HoverPreviewOptions.SectionName))
    .Validate(HoverPreviewOptions.HasPositiveWidth, "HoverPreview:Width must be greater than zero.")
    .Validate(HoverPreviewOptions.HasPositiveFrameRate, "HoverPreview:FrameRate must be greater than zero.")
    .Validate(HoverPreviewOptions.HasPositiveSegmentSeconds, "HoverPreview:SegmentSeconds must be greater than zero.")
    .Validate(HoverPreviewOptions.HasPositiveQueueCapacity, "HoverPreview:QueueCapacity must be greater than zero.")
    .ValidateOnStart();
builder.Services.AddSingleton<IVideoLibraryService, VideoLibraryService>();
builder.Services.AddSingleton<ThumbnailCache>();
builder.Services.AddSingleton<ThumbnailCoordinator>();
builder.Services.AddSingleton<IThumbnailJobQueue, ThumbnailJobQueue>();
builder.Services.AddSingleton<IVideoDurationProbe, FfprobeDurationProbe>();
builder.Services.AddSingleton<IVideoResolutionProbe, FfprobeResolutionProbe>();
builder.Services.AddSingleton<VideoMetadataCoordinator>();
builder.Services.AddSingleton<IThumbnailGenerator, FfmpegThumbnailGenerator>();
builder.Services.AddHostedService<ThumbnailBackgroundWorker>();
builder.Services.AddSingleton<HoverPreviewCache>();
builder.Services.AddSingleton<HoverPreviewCoordinator>();
builder.Services.AddSingleton<IHoverPreviewJobQueue, HoverPreviewJobQueue>();
builder.Services.AddSingleton<IHoverPreviewGenerator, FfmpegHoverPreviewGenerator>();
builder.Services.AddHostedService<HoverPreviewBackgroundWorker>();
builder.Services.AddSingleton<IVideoCutService, VideoCutService>();
builder.Services.AddSingleton<CutNamingService>();
builder.Services.AddSingleton<ICutJobQueue, CutJobQueue>();
builder.Services.AddSingleton<ICutGenerator, FfmpegCutGenerator>();
builder.Services.AddHostedService<CutBackgroundWorker>();
builder.Services.AddSingleton<IVideoCompositionService, VideoCompositionService>();
builder.Services.AddSingleton<CompositionNamingService>();
builder.Services.AddSingleton<ICompositionJobQueue, CompositionJobQueue>();
builder.Services.AddSingleton<ICompositionJobStatusStore, CompositionJobStatusStore>();
builder.Services.AddSingleton<IVideoCompositionProbe, FfprobeCompositionProbe>();
builder.Services.AddSingleton<ICompositionGenerator, FfmpegCompositionGenerator>();
builder.Services.AddHostedService<CompositionBackgroundWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapVideoEndpoints();
app.MapCutEndpoints();
app.MapCompositionEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(WebApp.Client._Imports).Assembly);

app.Run();

public partial class Program;
