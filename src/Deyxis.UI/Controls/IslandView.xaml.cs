using Deyxis.Core.Activities;
using Deyxis.Core.Island;
using Deyxis.Core.Settings;
using Deyxis.Providers.Lyrics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;

namespace Deyxis.UI.Controls;

public sealed partial class IslandView : UserControl
{
    private ActivitySnapshot? snapshot;
    private LyricsSnapshot lyrics = LyricsSnapshot.Empty;
    private IslandStateMachine? stateMachine;
    private bool expandOnHover = true;

    public IslandView()
    {
        InitializeComponent();
    }

    public event EventHandler? PresentationStateChanged;

    public event Func<IReadOnlyList<string>, Task>? FilesDropped;

    public event Func<Guid, Task>? FileDropConfirmRequested;

    public event Action<Guid>? FileDropCancelRequested;

    public event EventHandler? RevealRequested;

    public event EventHandler? SettingsRequested;

    public IslandViewModel ViewModel { get; } = new();

    public void ApplySettings(SettingsSnapshot settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        expandOnHover = settings.ExpandOnHover;
        Opacity = settings.Opacity;
        var cornerRadius = new CornerRadius(settings.CornerRadius);
        CapsuleSurface.CornerRadius = cornerRadius;
        ExpandedSurface.CornerRadius = cornerRadius;
        var color = settings.SurfaceMode switch
        {
            IslandSurfaceMode.Mica => Color.FromArgb(242, 30, 32, 38),
            IslandSurfaceMode.Acrylic => Color.FromArgb(220, 20, 23, 31),
            _ => Color.FromArgb(242, 11, 12, 16),
        };
        CapsuleSurface.Background = new SolidColorBrush(color);
        ExpandedSurface.Background = new SolidColorBrush(color);
    }

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

    public void SetPresentationState(IslandPresentationState state)
    {
        stateMachine?.SetPresentationState(state);
        RefreshPresentation();
    }

    private void RevealStrip_PointerEntered(
        object sender,
        Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) =>
        RevealRequested?.Invoke(this, EventArgs.Empty);

    private void CapsuleSurface_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (expandOnHover)
        {
            stateMachine?.PointerEntered();
        }
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

    private void SettingsButton_Click(object sender, RoutedEventArgs e) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

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

        var isHiddenEdge = ViewModel.PresentationState == IslandPresentationState.HiddenEdge;
        var isExpanded = ViewModel.PresentationState == IslandPresentationState.Expanded;
        RevealStrip.Visibility = isHiddenEdge ? Visibility.Visible : Visibility.Collapsed;
        CapsuleSurface.Visibility = isExpanded || isHiddenEdge ? Visibility.Collapsed : Visibility.Visible;
        ExpandedSurface.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
        HoverSummary.Visibility = ViewModel.PresentationState == IslandPresentationState.Hover
            ? Visibility.Visible
            : Visibility.Collapsed;

        PresentationStateChanged?.Invoke(this, EventArgs.Empty);
    }
}
