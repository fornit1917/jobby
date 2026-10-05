namespace Jobby.Dashboard.Client;

public enum DashboardMode
{
    Management,
    ReadOnly
}

public sealed class DashboardModeState
{
    private DashboardMode _mode = DashboardMode.Management;

    public DashboardMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value)
            {
                return;
            }

            _mode = value;
            Changed?.Invoke();
        }
    }

    public bool IsManagementView => _mode == DashboardMode.Management;

    public event Action? Changed;
}