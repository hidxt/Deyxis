using Microsoft.UI.Xaml;

namespace Deyxis.App;

public partial class App : Application
{
    private IslandWindow? islandWindow;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        islandWindow = new IslandWindow();
        islandWindow.ShowWithoutActivation();
    }
}
