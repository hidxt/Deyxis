using Deyxis.Core.Activities;

namespace Deyxis.Core.History;

public sealed record ActivityHistorySummary(
    string ProviderId,
    ActivityCategory Category,
    ActivityState State,
    string Title,
    DateTimeOffset Timestamp)
{
    public static ActivityHistorySummary FromActivity(Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        return new(
            activity.ProviderId,
            activity.Category,
            activity.State,
            activity.Title,
            activity.Timestamp);
    }
}
