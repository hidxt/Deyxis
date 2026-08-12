using Deyxis.Core.Activities;

namespace Deyxis.Core.History;

public sealed class ActivityHistoryRing
{
    public const int Capacity = 20;

    private readonly List<ActivityHistorySummary> entries = [];

    public ActivityHistoryRing()
    {
    }

    public ActivityHistoryRing(IEnumerable<ActivityHistorySummary> summaries)
    {
        ArgumentNullException.ThrowIfNull(summaries);
        entries.AddRange(summaries.Take(Capacity));
    }

    public IReadOnlyList<ActivityHistorySummary> Entries => entries.AsReadOnly();

    public void Add(Activity activity)
    {
        entries.Insert(0, ActivityHistorySummary.FromActivity(activity));
        if (entries.Count > Capacity)
        {
            entries.RemoveAt(Capacity);
        }
    }

    public void Clear() => entries.Clear();
}
