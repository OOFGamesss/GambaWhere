using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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

    public async Task<(EventCreateResponse? Created, string? Error)> PostEventAsync(PostEventRequest request)
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

                var body = await response.Content.ReadAsStringAsync();
                return (null, ReadProblemDetail(body));
            }

            var created = await response.Content.ReadFromJsonAsync<EventCreateResponse>(JsonOptions);
            return (created, created == null ? "The server returned an empty response." : null);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "POST /events failed.");
            return (null, null);
        }
    }

    private static string? ReadProblemDetail(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("detail", out var detail))
                return null;

            if (detail.ValueKind == JsonValueKind.String)
                return detail.GetString();

            if (detail.ValueKind == JsonValueKind.Array && detail.GetArrayLength() > 0)
                return detail[0].GetString();

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<EventResponse?> PutEventAsync(string eventId, string sessionToken, PutEventRequest request)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Put, $"events/id/{Uri.EscapeDataString(eventId)}")
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);

            var response = await _http.SendAsync(message);
            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("PUT /events/id/{Id} failed: {Status}", eventId, response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<EventResponse>(JsonOptions);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "PUT /events/id/{Id} failed.", eventId);
            return null;
        }
    }

    public async Task<EventBatchResponse?> PutEventsBatchAsync(PutEventsBatchRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Put, "events/batch")
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            };

            var response = await _http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("PUT /events/batch failed: {Status}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<EventBatchResponse>(JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
                _log.Warning(ex, "PUT /events/batch failed.");
            return null;
        }
    }

    public async Task<EventResponse?> PutEventByCharacterAsync(string characterName, string sessionToken, PutEventRequest request)
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

    public async Task DeleteEventAsync(string eventId, string sessionToken)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Delete, $"events/id/{Uri.EscapeDataString(eventId)}");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);

            var response = await _http.SendAsync(message);
            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("DELETE /events/id/{Id} failed: {Status}", eventId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "DELETE /events/id/{Id} failed.", eventId);
        }
    }
}
