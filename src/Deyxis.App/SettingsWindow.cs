using Deyxis.Core.History;
using Deyxis.Core.Settings;
using Deyxis.UI.History;
using Deyxis.UI.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Deyxis.App;

public sealed class SettingsWindow : Window
{
    private readonly SettingsViewModel settings;
    private readonly ActivityHistoryViewModel history = new();
    private readonly ComboBox surfaceMode = new();
    private readonly NumberBox islandWidth = NumberBox(240, 800);
    private readonly NumberBox cornerRadius = NumberBox(0, 64);
    private readonly NumberBox opacity = NumberBox(0.3, 1, 0.05);
    private readonly ToggleSwitch followActiveMonitor = new();
    private readonly ToggleSwitch expandOnHover = new();
    private readonly ToggleSwitch hideInFullscreen = new();
    private readonly ToggleSwitch doNotDisturb = new();
    private readonly ToggleSwitch showProviderHealth = new();
    private readonly StackPanel historyRows = new() { Spacing = 8 };

    public SettingsWindow(SettingsSnapshot snapshot, IEnumerable<ActivityHistorySummary> summaries)
    {
        settings = new SettingsViewModel(snapshot);
        settings.SettingsChanged += (_, args) => SettingsChanged?.Invoke(this, args);
        history.ClearRequested += (_, _) =>
        {
            RenderHistory();
            HistoryClearRequested?.Invoke(this, EventArgs.Empty);
        };
        history.Refresh(summaries);
        Title = "Deyxis Settings";
        Content = BuildContent();
        LoadControls();
        RenderHistory();
    }

    public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

    public event EventHandler? HistoryClearRequested;

    public void RefreshHistory(IEnumerable<ActivityHistorySummary> summaries)
    {
        ArgumentNullException.ThrowIfNull(summaries);
        var snapshot = summaries.ToArray();
        if (!DispatcherQueue.HasThreadAccess)
        {
            _ = DispatcherQueue.TryEnqueue(() => RefreshHistory(snapshot));
            return;
        }

        history.Refresh(snapshot);
        RenderHistory();
    }

    private UIElement BuildContent()
    {
        surfaceMode.ItemsSource = Enum.GetValues<IslandSurfaceMode>();
        var root = new StackPanel { Padding = new Thickness(24), Spacing = 14 };
        root.Children.Add(new TextBlock { Text = "Settings", FontSize = 26 });
        AddSetting(root, "Follow active monitor", followActiveMonitor);
        AddSetting(root, "Surface", surfaceMode);
        AddSetting(root, "Island width", islandWidth);
        AddSetting(root, "Corner radius", cornerRadius);
        AddSetting(root, "Opacity", opacity);
        AddSetting(root, "Expand on hover", expandOnHover);
        AddSetting(root, "Hide in fullscreen", hideInFullscreen);
        AddSetting(root, "Do not disturb", doNotDisturb);
        AddSetting(root, "Show provider health", showProviderHealth);

        var apply = new Button { Content = "Apply", HorizontalAlignment = HorizontalAlignment.Right };
        apply.Click += (_, _) => Apply();
        root.Children.Add(apply);
        root.Children.Add(new Border
        {
            Height = 1,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(48, 128, 128, 128)),
        });

        var header = new Grid { ColumnDefinitions = { new(), new() { Width = GridLength.Auto } } };
        header.Children.Add(new TextBlock { Text = "Recent activity", FontSize = 20 });
        var clear = new Button { Content = "Clear" };
        clear.Click += (_, _) => history.Clear();
        Grid.SetColumn(clear, 1);
        header.Children.Add(clear);
        root.Children.Add(header);
        root.Children.Add(historyRows);
        return new ScrollViewer { Content = root };
    }

    private static void AddSetting(Panel panel, string label, Control control)
    {
        var row = new Grid { ColumnSpacing = 18, ColumnDefinitions = { new(), new() { Width = new GridLength(260) } } };
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(control, 1);
        row.Children.Add(control);
        panel.Children.Add(row);
    }

    private static NumberBox NumberBox(double minimum, double maximum, double step = 1) => new()
    {
        Minimum = minimum,
        Maximum = maximum,
        SmallChange = step,
        SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
    };

    private void LoadControls()
    {
        followActiveMonitor.IsOn = settings.FollowActiveMonitor;
        surfaceMode.SelectedItem = settings.SurfaceMode;
        islandWidth.Value = settings.IslandWidth;
        cornerRadius.Value = settings.CornerRadius;
        opacity.Value = settings.Opacity;
        expandOnHover.IsOn = settings.ExpandOnHover;
        hideInFullscreen.IsOn = settings.HideInFullscreen;
        doNotDisturb.IsOn = settings.DoNotDisturb;
        showProviderHealth.IsOn = settings.ShowProviderHealth;
    }

    private void Apply()
    {
        settings.FollowActiveMonitor = followActiveMonitor.IsOn;
        settings.SurfaceMode = surfaceMode.SelectedItem is IslandSurfaceMode value ? value : SettingsSnapshot.Default.SurfaceMode;
        settings.IslandWidth = islandWidth.Value;
        settings.CornerRadius = cornerRadius.Value;
        settings.Opacity = opacity.Value;
        settings.ExpandOnHover = expandOnHover.IsOn;
        settings.HideInFullscreen = hideInFullscreen.IsOn;
        settings.DoNotDisturb = doNotDisturb.IsOn;
        settings.ShowProviderHealth = showProviderHealth.IsOn;
        settings.Apply();
    }

    private void RenderHistory()
    {
        historyRows.Children.Clear();
        if (history.IsEmpty)
        {
            historyRows.Children.Add(new TextBlock { Text = "No recent activity.", Opacity = 0.7 });
            return;
        }

        foreach (var row in history.Rows)
        {
            var card = new StackPanel { Spacing = 2 };
            card.Children.Add(new TextBlock { Text = row.Title });
            card.Children.Add(new TextBlock
            {
                Text = $"{row.ProviderId} · {row.Category} · {row.State} · {row.Timestamp}",
                Opacity = 0.7,
                FontSize = 12,
            });
            historyRows.Children.Add(card);
        }
    }
}
