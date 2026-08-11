namespace Deyxis.Core.Island;

public sealed class IslandStateMachine
{
    public IslandPresentationState Current { get; private set; } = IslandPresentationState.Idle;

    public void PointerEntered()
    {
        if (Current == IslandPresentationState.Idle)
        {
            Current = IslandPresentationState.Hover;
        }
    }

    public void PointerExited()
    {
        if (Current == IslandPresentationState.Hover)
        {
            Current = IslandPresentationState.Idle;
        }
    }

    public void ToggleExpanded() => Current = Current == IslandPresentationState.Expanded
        ? IslandPresentationState.Idle
        : IslandPresentationState.Expanded;

    public void Collapse() => Current = IslandPresentationState.Idle;
}
