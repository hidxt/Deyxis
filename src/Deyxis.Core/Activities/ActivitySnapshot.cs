namespace Deyxis.Core.Activities;

public sealed record ActivitySnapshot(IReadOnlyList<Activity> OrderedActivities);
