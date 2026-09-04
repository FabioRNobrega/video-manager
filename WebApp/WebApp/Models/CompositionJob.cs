namespace WebApp.Models;

internal sealed record CompositionJob(string JobId, IReadOnlyList<VideoFileEntry> OrderedSources);
