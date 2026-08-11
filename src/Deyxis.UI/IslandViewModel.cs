using Deyxis.Core.Activities;
using Deyxis.Core.Island;

namespace Deyxis.UI;

public sealed class IslandViewModel
{
    public Activity? PrimaryActivity { get; private set; }

    public IReadOnlyList<Activity> Queue { get; private set; } = [];

    public IslandPresentationState PresentationState { get; private set; }

    public void Refresh(ActivitySnapshot snapshot, IslandPresentationState state)
    {
        PrimaryActivity = snapshot.OrderedActivities.FirstOrDefault();
        Queue = snapshot.OrderedActivities.Skip(1).ToArray();
        PresentationState = state;
    }
}
