using Deyxis.Core.Settings;

namespace Deyxis.UI.Settings;

public sealed class SettingsChangedEventArgs(SettingsSnapshot settings) : EventArgs
{
    public SettingsSnapshot Settings { get; } = settings ?? throw new ArgumentNullException(nameof(settings));
}
