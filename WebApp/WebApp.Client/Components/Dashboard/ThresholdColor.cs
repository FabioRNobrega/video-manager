namespace WebApp.Client.Components.Dashboard;

internal static class ThresholdColor
{
    public const double WarningPercent = 70.0;
    public const double CriticalPercent = 90.0;

    public static string ProgressBarClass(double? percent) => percent switch
    {
        null => "text-bg-secondary",
        >= CriticalPercent => "text-bg-danger",
        >= WarningPercent => "text-bg-warning",
        _ => "text-bg-success",
    };

    public static string TextClass(double? percent) => percent switch
    {
        null => "text-body-secondary",
        >= CriticalPercent => "text-danger",
        >= WarningPercent => "text-warning",
        _ => "text-success",
    };
}
