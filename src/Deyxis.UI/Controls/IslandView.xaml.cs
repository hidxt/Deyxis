using Deyxis.Core.Activities;
using Deyxis.Core.Island;
using Deyxis.Providers.Lyrics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace Deyxis.UI.Controls;

public sealed partial class IslandView : UserControl
{
    private ActivitySnapshot? snapshot;
    private LyricsSnapshot lyrics = LyricsSnapshot.Empty;
    private IslandStateMachine? stateMachine;

    public IslandView()
    {
        InitializeComponent();
    }

    public event EventHandler? PresentationStateChanged;

    public event Func<IReadOnlyList<string>, Task>? FilesDropped;

    public event Func<Guid, Task>? FileDropConfirmRequested;

    public event Action<Guid>? FileDropCancelRequested;

    public IslandViewModel ViewModel { get; } = new();

    public void SetValidatedFileDrop(Guid activityId, Guid confirmationToken, string canonicalPath)
    {
        ViewModel.SetValidatedFileDrop(activityId, confirmationToken, canonicalPath);
        if (stateMachine?.Current != IslandPresentationState.Expanded)
        {
            stateMachine?.ToggleExpanded();
        }

        RefreshPresentation();
    }

    public void Bind(
        ActivitySnapshot activitySnapshot,
        IslandStateMachine presentationStateMachine,
        LyricsSnapshot? lyrics = null)
    {
        snapshot = activitySnapshot;
        this.lyrics = lyrics ?? LyricsSnapshot.Empty;
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

    private void Root_DragOver(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Validate image for wallpaper preview";
        e.DragUIOverride.IsContentVisible = false;
    }

    private async void Root_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems) || FilesDropped is null)
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        await FilesDropped(items.Select(item => item.Path).ToArray());
    }

    private async void ConfirmWallpaperButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.FileDropConfirmationToken is Guid confirmationToken &&
            FileDropConfirmRequested is not null)
        {
            await FileDropConfirmRequested(confirmationToken);
        }
    }

    private void CancelWallpaperButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.FileDropConfirmationToken is Guid confirmationToken)
        {
            FileDropCancelRequested?.Invoke(confirmationToken);
        }
    }

    private void RefreshPresentation()
    {
        if (snapshot is null || stateMachine is null)
        {
            return;
        }

        ViewModel.Refresh(snapshot, stateMachine.Current, lyrics);
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
