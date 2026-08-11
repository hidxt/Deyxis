using Deyxis.Core.Island;
using Xunit;

namespace Deyxis.Core.Tests;

public sealed class IslandStateMachineTests
{
    [Fact]
    public void Toggle_expanded_from_hover_enters_expanded_state()
    {
        var stateMachine = new IslandStateMachine();
        stateMachine.PointerEntered();

        stateMachine.ToggleExpanded();

        Assert.Equal(IslandPresentationState.Expanded, stateMachine.Current);
    }

    [Fact]
    public void Pointer_exited_from_hover_returns_to_idle()
    {
        var stateMachine = new IslandStateMachine();
        stateMachine.PointerEntered();

        stateMachine.PointerExited();

        Assert.Equal(IslandPresentationState.Idle, stateMachine.Current);
    }

    [Fact]
    public void Toggle_expanded_from_expanded_returns_to_idle()
    {
        var stateMachine = new IslandStateMachine();
        stateMachine.ToggleExpanded();

        stateMachine.ToggleExpanded();

        Assert.Equal(IslandPresentationState.Idle, stateMachine.Current);
    }

    [Fact]
    public void Pointer_exited_does_not_collapse_expanded_state()
    {
        var stateMachine = new IslandStateMachine();
        stateMachine.ToggleExpanded();

        stateMachine.PointerExited();

        Assert.Equal(IslandPresentationState.Expanded, stateMachine.Current);
    }

    [Fact]
    public void Collapse_returns_to_idle_from_expanded_state()
    {
        var stateMachine = new IslandStateMachine();
        stateMachine.ToggleExpanded();

        stateMachine.Collapse();

        Assert.Equal(IslandPresentationState.Idle, stateMachine.Current);
    }
}
