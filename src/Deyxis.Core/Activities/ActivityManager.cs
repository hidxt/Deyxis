using Deyxis.Core.Priority;

namespace Deyxis.Core.Activities;

public sealed class ActivityManager
{
    private readonly Dictionary<Guid, Activity> activities = [];
    private readonly ActivityPriorityPolicy priorityPolicy;

    public ActivityManager(ActivityPriorityPolicy? priorityPolicy = null)
    {
        this.priorityPolicy = priorityPolicy ?? new ActivityPriorityPolicy();
    }

    public bool Upsert(Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        if (activities.TryGetValue(activity.Id, out var existing) && existing == activity)
        {
            return false;
        }

        activities[activity.Id] = activity;
        return true;
    }

    public bool Remove(Guid activityId) => activities.Remove(activityId);

    public ActivitySnapshot Snapshot() => new(priorityPolicy.Order(activities.Values));
}
