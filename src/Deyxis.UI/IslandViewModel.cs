using System.ComponentModel;
using System.Runtime.CompilerServices;
using Deyxis.Core.Activities;
using Deyxis.Core.Island;

namespace Deyxis.UI;

public sealed class IslandViewModel : INotifyPropertyChanged
{
    private Activity? primaryActivity;
    private IReadOnlyList<Activity> queue = [];
    private IslandPresentationState presentationState;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Activity? PrimaryActivity
    {
        get => primaryActivity;
        private set => SetProperty(ref primaryActivity, value);
    }

    public IReadOnlyList<Activity> Queue
    {
        get => queue;
        private set => SetProperty(ref queue, value);
    }

    public IslandPresentationState PresentationState
    {
        get => presentationState;
        private set => SetProperty(ref presentationState, value);
    }

    public void Refresh(ActivitySnapshot snapshot, IslandPresentationState state)
    {
        PrimaryActivity = snapshot.OrderedActivities.FirstOrDefault();
        Queue = snapshot.OrderedActivities.Skip(1).ToArray();
        PresentationState = state;
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
