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
builder.Services.AddSingleton<IVideoLibraryService, VideoLibraryService>();

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
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapVideoEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(WebApp.Client._Imports).Assembly);

app.Run();

public partial class Program;
