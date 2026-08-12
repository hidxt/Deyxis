using Deyxis.Core.Activities;
using Deyxis.Core.Settings;
using Xunit;

namespace Deyxis.Core.Tests;

public sealed class DoNotDisturbPolicyTests
{
    [Fact]
    public void Waiting_bypasses_do_not_disturb_while_a_normal_autonomous_prompt_is_suppressed()
    {
        Assert.True(DoNotDisturbPolicy.AllowsPresentation(
            isEnabled: true,
            ActivityState.Waiting,
            ActivityPresentationRequest.AutonomousPrompt));

        Assert.False(DoNotDisturbPolicy.AllowsPresentation(
            isEnabled: true,
            ActivityState.Running,
            ActivityPresentationRequest.AutonomousPrompt));
    }

    [Theory]
    [InlineData(ActivityState.Running, ActivityPresentationRequest.ManualOpen)]
    [InlineData(ActivityState.Failed, ActivityPresentationRequest.AutonomousExpansion)]
    public void Manual_open_and_failed_activity_bypass_do_not_disturb(
        ActivityState state,
        ActivityPresentationRequest request)
    {
        Assert.True(DoNotDisturbPolicy.AllowsPresentation(isEnabled: true, state, request));
    }

    [Fact]
    public void Disabled_do_not_disturb_allows_a_normal_autonomous_prompt()
    {
        Assert.True(DoNotDisturbPolicy.AllowsPresentation(
            isEnabled: false,
            ActivityState.Running,
            ActivityPresentationRequest.AutonomousPrompt));
    }
}
