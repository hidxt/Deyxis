namespace Deyxis.Core.Activities;

public sealed record Activity(
    Guid Id,
    string ProviderId,
    ActivityCategory Category,
    ActivityState State,
    int Priority,
    string Title,
    string Description,
    double? Progress,
    DateTimeOffset Timestamp);
