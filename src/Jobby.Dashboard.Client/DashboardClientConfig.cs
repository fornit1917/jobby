using System.Net.Http.Json;
using Jobby.Core.Models.Dashboard;

namespace Jobby.Dashboard.Client;

public enum DashboardClientConfigLoadState
{
    Loading,
    Ready,
    RequiresAuthentication,
    Failed
}

public sealed class DashboardClientConfig
{
    public TimeSpan? RefreshInterval { get; private set; } = TimeSpan.FromSeconds(5);
    public int StaleServerThresholdSeconds { get; private set; } = 300;
    public bool ReadOnly { get; private set; }
    public bool ManagementEnabled { get; private set; }
    public string? ManagementRequestHeaderName { get; private set; }
    public string? ManagementRequestToken { get; private set; }
    public int Version { get; private set; }

    public string? DefaultTimeZoneId { get; private set; }
    public DashboardDateStyle DefaultDateStyle { get; private set; } = DashboardDateStyle.Iso;
    public DashboardTimeStyle DefaultTimeStyle { get; private set; } = DashboardTimeStyle.H24WithSeconds;
    public DashboardLanguage DefaultLanguage { get; private set; } = DashboardLanguage.English;

    private TimeSpan _serverClockOffset = TimeSpan.Zero;

    public DateTime ServerUtcNow => DateTime.UtcNow + _serverClockOffset;

    public DashboardClientConfigLoadState LoadState { get; private set; } = DashboardClientConfigLoadState.Loading;

    public event Action? Changed;

    public async Task LoadAsync(HttpClient http, CancellationToken cancellationToken = default)
    {
        SetLoadState(DashboardClientConfigLoadState.Loading);
        try
        {
            using var response = await http.GetAsync("api/config", cancellationToken);
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                SetLoadState(DashboardClientConfigLoadState.RequiresAuthentication);
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                SetLoadState(DashboardClientConfigLoadState.Failed);
                return;
            }

            var dto = await response.Content.ReadFromJsonAsync<JobbyDashboardClientConfigDto>(cancellationToken);
            if (dto is not null)
            {
                Apply(dto);
                return;
            }

            SetLoadState(DashboardClientConfigLoadState.Failed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            SetLoadState(DashboardClientConfigLoadState.Failed);
        }
    }

    private void Apply(JobbyDashboardClientConfigDto dto)
    {
        RefreshInterval = dto.RefreshIntervalSeconds is { } s ? TimeSpan.FromSeconds(s) : null;
        StaleServerThresholdSeconds = dto.StaleServerThresholdSeconds;
        ReadOnly = dto.ReadOnly;
        ManagementEnabled = dto.ManagementEnabled;
        ManagementRequestHeaderName = dto.ManagementRequestHeaderName;
        ManagementRequestToken = dto.ManagementRequestToken;
        DefaultTimeZoneId = dto.DefaultTimeZoneId;
        DefaultDateStyle = dto.DefaultDateStyle;
        DefaultTimeStyle = dto.DefaultTimeStyle;
        DefaultLanguage = dto.DefaultLanguage;
        _serverClockOffset = dto.ServerTimeUtc == default
            ? TimeSpan.Zero
            : DateTime.SpecifyKind(dto.ServerTimeUtc, DateTimeKind.Utc) - DateTime.UtcNow;
        LoadState = DashboardClientConfigLoadState.Ready;
        Version++;
        Changed?.Invoke();
    }

    private void SetLoadState(DashboardClientConfigLoadState loadState)
    {
        LoadState = loadState;
        Changed?.Invoke();
    }
}