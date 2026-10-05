using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using Jobby.Core.Models.Dashboard;
using Microsoft.AspNetCore.Components;

namespace Jobby.Dashboard.Client;

public sealed class DashboardApiClient
{
    private readonly HttpClient _http;
    private readonly NavigationManager _nav;
    private readonly DashboardClientConfig _config;

    public DashboardApiClient(HttpClient http, NavigationManager nav, DashboardClientConfig config)
    {
        _http = http;
        _nav = nav;
        _config = config;
    }

    public async Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        return await _http.GetFromJsonAsync<DashboardStatsDto>("api/stats", cancellationToken)
               ?? throw new InvalidOperationException("The stats endpoint returned an empty response.");
    }

    public async Task<PagedResult<DashboardJobListItemDto>> GetJobsAsync(DashboardJobsQuery query,
        CancellationToken cancellationToken = default)
    {
        return await _http.GetFromJsonAsync<PagedResult<DashboardJobListItemDto>>(JobsUri(query), cancellationToken)
               ?? throw new InvalidOperationException("The jobs endpoint returned an empty response.");
    }

    public async Task<DashboardJobDetailsDto?> GetJobByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var resp = await _http.GetAsync($"api/jobs/{id}", cancellationToken);

        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<DashboardJobDetailsDto>(cancellationToken);
    }

    public async Task<PagedResult<DashboardRecurrentJobDto>> GetRecurrentJobsAsync(DashboardRecurrentJobsQuery query,
        CancellationToken cancellationToken = default)
    {
        return await _http.GetFromJsonAsync<PagedResult<DashboardRecurrentJobDto>>(RecurrentUri(query), cancellationToken)
               ?? throw new InvalidOperationException("The recurrent endpoint returned an empty response.");
    }

    public async Task<IReadOnlyList<DashboardServerDto>> GetServersAsync(CancellationToken cancellationToken = default)
    {
        return await _http.GetFromJsonAsync<IReadOnlyList<DashboardServerDto>>("api/servers", cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<DashboardQueueDto>> GetQueuesAsync(CancellationToken cancellationToken = default)
    {
        return await _http.GetFromJsonAsync<IReadOnlyList<DashboardQueueDto>>("api/queues", cancellationToken) ?? [];
    }

    public async Task<PagedResult<LockedGroupDto>> GetLockedGroupsAsync(LockedGroupsQuery query,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["page"] = query.Page.ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = query.PageSize.ToString(CultureInfo.InvariantCulture),
        };
        var uri = _nav.GetUriWithQueryParameters("api/locked-groups", parameters);
        return await _http.GetFromJsonAsync<PagedResult<LockedGroupDto>>(uri, cancellationToken)
               ?? throw new InvalidOperationException("The locked-groups endpoint returned an empty response.");
    }

    public async Task<LockedGroupDetailsDto?> GetLockedGroupAsync(string groupId, CancellationToken cancellationToken = default)
    {
        var uri = _nav.GetUriWithQueryParameters("api/locked-groups/detail",
            new Dictionary<string, object?> { ["groupId"] = groupId });
        using var resp = await _http.GetAsync(uri, cancellationToken);
        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<LockedGroupDetailsDto>(cancellationToken);
    }

    public async Task<RequestGroupUnlockResultDto> RequestGroupUnlockAsync(string groupId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_config.ManagementRequestHeaderName) ||
            string.IsNullOrWhiteSpace(_config.ManagementRequestToken))
        {
            throw new InvalidOperationException("Dashboard management request token has not been loaded.");
        }

        using (var request = new HttpRequestMessage(HttpMethod.Post, "api/locked-groups/unlock-request"))
        {
            request.Content = JsonContent.Create(new UnlockGroupRequest(groupId));
            request.Headers.Add(_config.ManagementRequestHeaderName, _config.ManagementRequestToken);
            using var resp = await _http.SendAsync(request, cancellationToken);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<RequestGroupUnlockResultDto>(cancellationToken)
                   ?? throw new InvalidOperationException("The unlock-request endpoint returned an empty response.");
        }
    }

    public Task<bool> TriggerJobAsync(Guid id, CancellationToken cancellationToken = default)
        => SendActionAsync(HttpMethod.Post, $"api/jobs/{id}/trigger", cancellationToken);

    public Task<bool> RetryJobAsync(Guid id, CancellationToken cancellationToken = default)
        => SendActionAsync(HttpMethod.Post, $"api/jobs/{id}/retry", cancellationToken);

    public Task<bool> DeleteJobAsync(Guid id, CancellationToken cancellationToken = default)
        => SendActionAsync(HttpMethod.Delete, $"api/jobs/{id}", cancellationToken);

    public Task<bool> DeleteRecurrentJobAsync(Guid id, CancellationToken cancellationToken = default)
        => SendActionAsync(HttpMethod.Delete, $"api/recurrent/{id}", cancellationToken);

    private async Task<bool> SendActionAsync(HttpMethod method, string uri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri);
        if (string.IsNullOrWhiteSpace(_config.ManagementRequestHeaderName) ||
            string.IsNullOrWhiteSpace(_config.ManagementRequestToken))
        {
            throw new InvalidOperationException("Dashboard management request token has not been loaded.");
        }

        request.Headers.Add(_config.ManagementRequestHeaderName, _config.ManagementRequestToken);
        using var resp = await _http.SendAsync(request, cancellationToken);

        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        resp.EnsureSuccessStatusCode();
        return true;
    }

    private string JobsUri(DashboardJobsQuery query)
    {
        var parameters = new Dictionary<string, object?>();

        if (query.Status is { } st)
        {
            parameters["status"] = st.ToString();
        }

        if (!string.IsNullOrWhiteSpace(query.QueueName))
        {
            parameters["queueName"] = query.QueueName;
        }

        if (query.Statuses is { Count: > 0 })
        {
            parameters["statuses"] = string.Join(",", query.Statuses);
        }

        if (!string.IsNullOrWhiteSpace(query.JobNameSearch))
        {
            parameters["jobNameSearch"] = query.JobNameSearch;
        }

        if (query.CreatedFrom is { } cf)
        {
            parameters["createdFrom"] = cf.ToString("o", CultureInfo.InvariantCulture);
        }

        if (query.CreatedTo is { } ctv)
        {
            parameters["createdTo"] = ctv.ToString("o", CultureInfo.InvariantCulture);
        }

        parameters["page"] = query.Page.ToString(CultureInfo.InvariantCulture);
        parameters["pageSize"] = query.PageSize.ToString(CultureInfo.InvariantCulture);
        parameters["sortBy"] = query.SortBy.ToString();
        parameters["sortDescending"] = query.SortDescending.ToString();

        return _nav.GetUriWithQueryParameters("api/jobs", parameters);
    }

    private string RecurrentUri(DashboardRecurrentJobsQuery query)
    {
        var parameters = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(query.JobNameSearch))
        {
            parameters["jobNameSearch"] = query.JobNameSearch;
        }

        parameters["page"] = query.Page.ToString(CultureInfo.InvariantCulture);
        parameters["pageSize"] = query.PageSize.ToString(CultureInfo.InvariantCulture);

        return _nav.GetUriWithQueryParameters("api/recurrent", parameters);
    }
}
