using System.ComponentModel;
using System.Runtime.CompilerServices;
using Deyxis.Core.History;

namespace Deyxis.UI.History;

public sealed class ActivityHistoryViewModel : INotifyPropertyChanged
{
    private IReadOnlyList<ActivityHistoryRow> rows = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? ClearRequested;

    public IReadOnlyList<ActivityHistoryRow> Rows
    {
        get => rows;
        private set
        {
            rows = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    public bool IsEmpty => Rows.Count == 0;

    public void Refresh(IEnumerable<ActivityHistorySummary> summaries)
    {
        ArgumentNullException.ThrowIfNull(summaries);
        Rows = summaries
            .Select(summary => new ActivityHistoryRow(
                summary.ProviderId,
                summary.Category.ToString(),
                summary.State.ToString(),
                summary.Title,
                summary.Timestamp.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'")))
            .ToArray();
    }

    public void Clear()
    {
        Rows = [];
        ClearRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
