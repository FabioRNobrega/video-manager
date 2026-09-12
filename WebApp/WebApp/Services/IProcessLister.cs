namespace WebApp.Services;

internal interface IProcessLister
{
    IReadOnlyList<string> GetRunningProcessNames();
}
