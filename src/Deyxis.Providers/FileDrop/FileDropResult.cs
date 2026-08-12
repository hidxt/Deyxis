namespace Deyxis.Providers.FileDrop;

public sealed record FileDropResult(
    bool Accepted,
    Guid ActivityId,
    Guid ConfirmationToken,
    FileDropRejection? Rejection)
{
    internal static FileDropResult Accept(Guid activityId, Guid confirmationToken) =>
        new(true, activityId, confirmationToken, null);

    internal static FileDropResult Reject(Guid activityId, FileDropRejection rejection) =>
        new(false, activityId, Guid.Empty, rejection);
}
