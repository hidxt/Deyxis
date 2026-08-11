namespace Deyxis.PluginSdk;

public interface IActivityProvider
{
    string Id { get; }

    ProviderHealth Health { get; }

    void Start();

    void Stop();
}
