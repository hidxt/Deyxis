using Deyxis.Core.Priority;

namespace Deyxis.Core.Activities;

public sealed class ActivityManager
{
    private readonly Dictionary<Guid, Activity> activities = [];
    private readonly ActivityPriorityPolicy priorityPolicy = new();

    public void Upsert(Activity activity) => activities[activity.Id] = activity;

    public ActivitySnapshot Snapshot() => new(priorityPolicy.Order(activities.Values));
}
