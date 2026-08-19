using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GambaWhere.Models;

namespace GambaWhere.API;

/// <summary>GambaWhereClient scheduled session endpoints: list, paged list, create, edit, advance to the next occurrence, and delete a whole series.</summary>
public partial class GambaWhereClient
{
    private const int CreateAttempts = 3;

    private static readonly TimeSpan CreateRetryDelay = TimeSpan.FromSeconds(2);

    public const string UnconfirmedCreateMessage =
        "The server never confirmed the schedule, so it may still have been created. Check Upcoming Gamba Events before booking it again.";

    public async Task<ScheduledEventResponse[]?> GetScheduledAsync(
        string? characterName = null,
        CancellationToken cancellationToken = default)
    {
        var url = new StringBuilder("scheduled");
        if (!string.IsNullOrEmpty(characterName))
            url.Append("?character_name=").Append(Uri.EscapeDataString(characterName));

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, url.ToString());

            var response = await _http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("GET /scheduled failed: {Status}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ScheduledEventResponse[]>(JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
                _log.Warning(ex, "GET /scheduled failed.");
            return null;
        }
    }

    public async Task<ScheduledEventPageResponse?> GetScheduledPageAsync(
        int page,
        int pageSize,
        string? sort,
        IReadOnlyCollection<string>? gameTypes,
        IReadOnlyCollection<string>? dataCentres,
        CancellationToken cancellationToken = default)
    {
        var url = new StringBuilder("scheduled/page?page=").Append(page).Append("&page_size=").Append(pageSize);

        if (!string.IsNullOrEmpty(sort))
            url.Append("&sort=").Append(Uri.EscapeDataString(sort));

        if (gameTypes != null)
            foreach (var gameType in gameTypes)
                url.Append("&game_types=").Append(Uri.EscapeDataString(gameType));

        if (dataCentres != null)
            foreach (var dataCentre in dataCentres)
                url.Append("&data_centres=").Append(Uri.EscapeDataString(dataCentre));

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, url.ToString());

            var response = await _http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("GET /scheduled/page failed: {Status}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ScheduledEventPageResponse>(JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
                _log.Warning(ex, "GET /scheduled/page failed.");
            return null;
        }
    }

    public async Task<(ScheduledEventCreateResponse? Created, string? Error)> PostScheduledAsync(PostScheduledEventRequest request)
    {
        for (var attempt = 1; ; attempt++)
        {
            var (created, error, retryable) = await TryPostScheduledAsync(request);

            if (created != null)
                return (created, null);

            if (!retryable || attempt >= CreateAttempts || string.IsNullOrEmpty(request.RequestId))
                return (null, error);

            _log.Warning(
                "POST /scheduled attempt {Attempt} of {Total} did not confirm; retrying with the same request id.",
                attempt, CreateAttempts);

            await Task.Delay(CreateRetryDelay * attempt);
        }
    }

    private async Task<(ScheduledEventCreateResponse? Created, string? Error, bool Retryable)> TryPostScheduledAsync(
        PostScheduledEventRequest request)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "scheduled")
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            };

            var response = await _http.SendAsync(message);
            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("POST /scheduled failed: {Status}", response.StatusCode);

                var body = await response.Content.ReadAsStringAsync();
                return (null, ReadProblemDetail(body), (int)response.StatusCode >= 500);
            }

            var created = await response.Content.ReadFromJsonAsync<ScheduledEventCreateResponse>(JsonOptions);
            if (created != null)
                return (created, null, false);

            _log.Error("POST /scheduled returned {Status} with an unreadable body; the schedule may exist on the server.", response.StatusCode);
            return (null, UnconfirmedCreateMessage, true);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "POST /scheduled failed before the server's answer could be read.");
            return (null, UnconfirmedCreateMessage, true);
        }
    }

    public async Task<(ScheduledEventResponse? Updated, string? Error)> PutScheduledAsync(
        string scheduleId,
        string sessionToken,
        PutScheduledEventRequest request)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Put, $"scheduled/{Uri.EscapeDataString(scheduleId)}")
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);

            var response = await _http.SendAsync(message);
            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("PUT /scheduled/{Id} failed: {Status}", scheduleId, response.StatusCode);

                var body = await response.Content.ReadAsStringAsync();
                return (null, ReadProblemDetail(body));
            }

            var updated = await response.Content.ReadFromJsonAsync<ScheduledEventResponse>(JsonOptions);
            return (updated, updated == null ? "The server returned an empty response." : null);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "PUT /scheduled/{Id} failed.", scheduleId);
            return (null, "Could not reach the server. Check the log for details.");
        }
    }

    public async Task<(ScheduledAdvanceResponse? Result, bool Gone)> AdvanceScheduledAsync(
        string scheduleId,
        string sessionToken,
        DateTime occurrence)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, $"scheduled/{Uri.EscapeDataString(scheduleId)}/advance")
            {
                Content = JsonContent.Create(new AdvanceScheduledRequest { Occurrence = occurrence }, options: JsonOptions)
            };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);

            var response = await _http.SendAsync(message);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return (null, true);

            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("POST /scheduled/{Id}/advance failed: {Status}", scheduleId, response.StatusCode);
                return (null, false);
            }

            return (await response.Content.ReadFromJsonAsync<ScheduledAdvanceResponse>(JsonOptions), false);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "POST /scheduled/{Id}/advance failed.", scheduleId);
            return (null, false);
        }
    }

    public async Task<bool> DeleteScheduledAsync(string scheduleId, string sessionToken)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Delete, $"scheduled/{Uri.EscapeDataString(scheduleId)}");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);

            var response = await _http.SendAsync(message);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return true;

            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("DELETE /scheduled/{Id} failed: {Status}", scheduleId, response.StatusCode);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "DELETE /scheduled/{Id} failed.", scheduleId);
            return false;
        }
    }
}
