using WebApp.Models;

namespace WebApp.Services;

/// <summary>
/// Pure fade-position calculation logic (FR10): no I/O, easily unit-tested directly.
/// First clip fades out only, middle clips fade in and out, last clip fades in only.
/// Each fade is clamped to at most half its own clip's duration so it never overlaps itself.
/// </summary>
internal static class CompositionFadePlanner
{
    public static IReadOnlyList<CompositionFadePlan> Plan(
        IReadOnlyList<TimeSpan> durations, TimeSpan transitionDuration)
    {
        var plans = new List<CompositionFadePlan>(durations.Count);

        for (var index = 0; index < durations.Count; index++)
        {
            var duration = durations[index];
            var halfDuration = TimeSpan.FromTicks(duration.Ticks / 2);
            var fadeDuration = transitionDuration < halfDuration ? transitionDuration : halfDuration;

            var isFirst = index == 0;
            var isLast = index == durations.Count - 1;

            TimeSpan? fadeInStart = null;
            TimeSpan? fadeInDuration = null;
            TimeSpan? fadeOutStart = null;
            TimeSpan? fadeOutDuration = null;

            if (!isFirst)
            {
                fadeInStart = TimeSpan.Zero;
                fadeInDuration = fadeDuration;
            }

            if (!isLast)
            {
                fadeOutStart = duration - fadeDuration;
                fadeOutDuration = fadeDuration;
            }

            plans.Add(new CompositionFadePlan(fadeInStart, fadeInDuration, fadeOutStart, fadeOutDuration));
        }

        return plans;
    }
}
