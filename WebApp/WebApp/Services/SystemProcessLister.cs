using System.Diagnostics;

namespace WebApp.Services;

internal sealed class SystemProcessLister : IProcessLister
{
    public IReadOnlyList<string> GetRunningProcessNames()
    {
        try
        {
            return Process.GetProcesses().Select(process => process.ProcessName).ToList();
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return [];
        }
    }
}
