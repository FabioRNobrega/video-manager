namespace WebApp.Models;

internal sealed record CompositionFadePlan(
    TimeSpan? FadeInStart,
    TimeSpan? FadeInDuration,
    TimeSpan? FadeOutStart,
    TimeSpan? FadeOutDuration);
