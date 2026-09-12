namespace WebApp.Client.Models;

public sealed record DashboardNetworkInterfaceDto(
    string Name,
    long RxBytes,
    long TxBytes,
    long Errors,
    long Drops);
