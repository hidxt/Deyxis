using Deyxis.Core.Activities;

namespace Deyxis.Core.Settings;

public static class DoNotDisturbPolicy
{
    public static bool AllowsPresentation(
        bool isEnabled,
        ActivityState state,
        ActivityPresentationRequest request) =>
        !isEnabled ||
        request == ActivityPresentationRequest.ManualOpen ||
        state is ActivityState.Waiting or ActivityState.Failed;
}
