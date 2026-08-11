using Deyxis.Core.Activities;
using Deyxis.Core.Island;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Deyxis.UI.Controls;

public sealed partial class IslandView : UserControl
{
    private ActivitySnapshot? snapshot;
    private IslandStateMachine? stateMachine;

    public IslandView()
    {
        InitializeComponent();
    }

    public event EventHandler? PresentationStateChanged;

    public IslandViewModel ViewModel { get; } = new();

    public void Bind(ActivitySnapshot activitySnapshot, IslandStateMachine presentationStateMachine)
    {
        snapshot = activitySnapshot;
        stateMachine = presentationStateMachine;
        RefreshPresentation();
    }

    private void CapsuleSurface_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        stateMachine?.PointerEntered();
        RefreshPresentation();
    }

    private void CapsuleSurface_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        stateMachine?.PointerExited();
        RefreshPresentation();
    }

    private void CapsuleButton_Click(object sender, RoutedEventArgs e)
    {
        stateMachine?.ToggleExpanded();
        RefreshPresentation();
    }

    private void CollapseButton_Click(object sender, RoutedEventArgs e)
    {
        stateMachine?.Collapse();
        RefreshPresentation();
    }

    private void RefreshPresentation()
    {
        if (snapshot is null || stateMachine is null)
        {
            return;
        }

        ViewModel.Refresh(snapshot, stateMachine.Current);
        DataContext = ViewModel;

        var primary = ViewModel.PrimaryActivity;
        CapsuleTitle.Text = primary is null ? "Deyxis" : $"{primary.Title} · {primary.State}";
        HoverSummary.Text = primary?.Description ?? "No active work";

        var isExpanded = ViewModel.PresentationState == IslandPresentationState.Expanded;
        CapsuleSurface.Visibility = isExpanded ? Visibility.Collapsed : Visibility.Visible;
        ExpandedSurface.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
        HoverSummary.Visibility = ViewModel.PresentationState == IslandPresentationState.Hover
            ? Visibility.Visible
            : Visibility.Collapsed;

        PresentationStateChanged?.Invoke(this, EventArgs.Empty);
    }
}
