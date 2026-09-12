namespace WebApp.Client.Components.Dashboard;

internal static class DashboardFormat
{
    public static string CssPercent(double? percent) =>
        Math.Clamp(percent ?? 0, 0, 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture);

    public static string Bytes(long? bytes)
    {
        if (bytes is null)
        {
            return "unavailable";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size = (double)Math.Max(0, bytes.Value);
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{size:0} {units[unit]}" : $"{size:0.#} {units[unit]}";
    }

    public static string BytesPerSecond(double? bytesPerSecond) =>
        bytesPerSecond is null ? "unavailable" : $"{Bytes((long)bytesPerSecond.Value)}/s";

    public static string Duration(double? seconds)
    {
        if (seconds is null)
        {
            return "unavailable";
        }

        var span = TimeSpan.FromSeconds(Math.Max(0, seconds.Value));
        return span.TotalDays >= 1
            ? $"{(int)span.TotalDays}d {span.Hours}h {span.Minutes}m"
            : $"{span.Hours}h {span.Minutes}m";
    }

    public static string Percent(double? percent) => percent is null ? "unavailable" : $"{percent:0}%";
}
