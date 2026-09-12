namespace WebApp.Services;

internal interface IActiveClientTracker
{
    void Track(string clientId);

    int CountActive();
}
