using Deyxis.Core.Activities;

namespace Deyxis.PluginSdk;

public interface IActivityProvider
{
    string Id { get; }

    IReadOnlyList<Activity> GetActivities();
}
