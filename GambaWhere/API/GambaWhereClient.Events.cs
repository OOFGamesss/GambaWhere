using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GambaWhere.Models;

namespace GambaWhere.API;

/// <summary>GambaWhereClient event endpoints: list, paged list, create, update, and delete events.</summary>
public partial class GambaWhereClient
{
    public async Task<EventResponse[]?> GetEventsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, "events");

            var response = await _http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("GET /events failed: {Status}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<EventResponse[]>(JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
                _log.Warning(ex, "GET /events failed.");
            return null;
        }
    }

    public async Task<EventPageResponse?> GetEventsPageAsync(
        int page,
        int pageSize,
        string? sort,
        IReadOnlyCollection<string>? gameTypes,
        IReadOnlyCollection<string>? dataCentres,
        CancellationToken cancellationToken = default)
    {
        var url = new StringBuilder("events/page?page=").Append(page).Append("&page_size=").Append(pageSize);

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
                _log.Warning("GET /events/page failed: {Status}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<EventPageResponse>(JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "GET /events/page failed.");
            return null;
        }
    }

    public async Task<EventCreateResponse?> PostEventAsync(PostEventRequest request)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "events")
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            };

            var response = await _http.SendAsync(message);
            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("POST /events failed: {Status}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<EventCreateResponse>(JsonOptions);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "POST /events failed.");
            return null;
        }
    }

    public async Task<EventResponse?> PutEventAsync(string characterName, string sessionToken, PutEventRequest request)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Put, $"events/{Uri.EscapeDataString(characterName)}")
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);

            var response = await _http.SendAsync(message);
            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("PUT /events/{Name} failed: {Status}", characterName, response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<EventResponse>(JsonOptions);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "PUT /events/{Name} failed.", characterName);
            return null;
        }
    }

    public async Task DeleteEventAsync(string characterName, string sessionToken)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Delete, $"events/{Uri.EscapeDataString(characterName)}");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);

            var response = await _http.SendAsync(message);
            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("DELETE /events/{Name} failed: {Status}", characterName, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "DELETE /events/{Name} failed.", characterName);
        }
    }
}
