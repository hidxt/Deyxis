using Deyxis.Core.Activities;

namespace Deyxis.Core.Priority;

public sealed class ActivityPriorityPolicy
{
    public IReadOnlyList<Activity> Order(IEnumerable<Activity> activities) => activities
        .OrderByDescending(GetEffectivePriority)
        .ThenByDescending(activity => activity.Timestamp)
        .ThenBy(activity => activity.Id)
        .ToArray();

    private static int GetEffectivePriority(Activity activity) => activity.State switch
    {
        ActivityState.Waiting => 600,
        ActivityState.Failed => 500,
        ActivityState.Running => 300,
        ActivityState.Thinking => 200,
        ActivityState.Completed => 100,
        _ => 0,
    };
}
