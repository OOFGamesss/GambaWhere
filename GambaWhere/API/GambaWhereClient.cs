using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using GambaWhere.API.Models;

namespace GambaWhere.API;

/// <summary>HTTP client for the GambaWhere API covering the event and recruitment endpoints.</summary>
public class GambaWhereClient : IDisposable
{
    private const string BaseUrl = "https://api.oofgames.fyi/v1/";

    private readonly HttpClient _http;
    private readonly IPluginLog _log;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GambaWhereClient(IPluginLog log)
    {
        _log = log;
        _http = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<string[]> GetVenuesAsync()
    {
        try
        {
            var response = await _http.GetAsync("venues");
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("GET /venues returned unexpected status ({Status}): {Body}", response.StatusCode, body);
                return Array.Empty<string>();
            }

            var result = System.Text.Json.JsonSerializer.Deserialize<string[]>(body, JsonOptions);
            return result ?? Array.Empty<string>();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "GET /venues exception.");
            return Array.Empty<string>();
        }
    }

    public async Task<EventResponse[]?> GetEventsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.GetAsync("events", cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("GET /events returned unexpected status ({Status}): {Body}", response.StatusCode, body);
                return null;
            }

            return System.Text.Json.JsonSerializer.Deserialize<EventResponse[]>(body, JsonOptions)
                   ?? Array.Empty<EventResponse>();
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "GET /events exception.");
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
        try
        {
            var url = new StringBuilder("events/page?page=")
                .Append(page)
                .Append("&page_size=")
                .Append(pageSize);

            if (!string.IsNullOrEmpty(sort))
                url.Append("&sort=").Append(Uri.EscapeDataString(sort));

            if (gameTypes != null)
                foreach (var g in gameTypes)
                    url.Append("&game_types=").Append(Uri.EscapeDataString(g));

            if (dataCentres != null)
                foreach (var dc in dataCentres)
                    url.Append("&data_centres=").Append(Uri.EscapeDataString(dc));

            var response = await _http.GetAsync(url.ToString(), cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("GET /events/page returned unexpected status ({Status}): {Body}", response.StatusCode, body);
                return null;
            }

            return System.Text.Json.JsonSerializer.Deserialize<EventPageResponse>(body, JsonOptions);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "GET /events/page exception.");
            return null;
        }
    }

    public async Task<EventCreateResponse?> PostEventAsync(PostEventRequest request)
    {
        try
        {
            var hasPicture = !string.IsNullOrEmpty(request.ProfilePictureB64);
            var redacted = new PostEventRequest
            {
                CharacterName = request.CharacterName,
                Location = request.Location,
                Rules = request.Rules,
                Game = request.Game,
                Description = request.Description,
                VenueName = request.VenueName,
                ProfilePictureB64 = hasPicture ? "<base64 omitted>" : null,
                ProfileImageUrl = request.ProfileImageUrl,
                Bio = request.Bio,
                PreferredGames = request.PreferredGames,
                BoosterKey = string.IsNullOrEmpty(request.BoosterKey) ? null : "<key omitted>"
            };
            _log.Information("POST /events payload: {Payload}", System.Text.Json.JsonSerializer.Serialize(redacted, JsonOptions));
            var response = await _http.PostAsJsonAsync("events", request, JsonOptions);

            if (response.StatusCode == System.Net.HttpStatusCode.RequestEntityTooLarge && hasPicture)
            {
                var body413 = await response.Content.ReadAsStringAsync();
                _log.Warning("POST /events returned 413 (body exceeded a server or proxy size limit). Response: {Body}. Retrying without the picture.", body413);
                request.ProfilePictureB64 = null;
                response = await _http.PostAsJsonAsync("events", request, JsonOptions);
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _log.Warning("POST /events returned unexpected status ({Status}): {Body}", response.StatusCode, body);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<EventCreateResponse>(JsonOptions);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Exception during POST /events.");
            return null;
        }
    }

    public async Task<EventResponse?> PutEventAsync(string characterName, string sessionToken, PutEventRequest request)
    {
        try
        {
            var encoded = Uri.EscapeDataString(characterName);
            using var message = new HttpRequestMessage(HttpMethod.Put, $"events/{encoded}");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);
            message.Content = JsonContent.Create(request, options: JsonOptions);

            var response = await _http.SendAsync(message);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _log.Warning("PUT /events/{Name} returned unexpected status ({Status}): {Body}", characterName, response.StatusCode, body);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<EventResponse>(JsonOptions);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Exception during PUT /events/{Name}.", characterName);
            return null;
        }
    }

    public async Task DeleteEventAsync(string characterName, string sessionToken)
    {
        try
        {
            var encoded = Uri.EscapeDataString(characterName);
            using var message = new HttpRequestMessage(HttpMethod.Delete, $"events/{encoded}");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);

            var response = await _http.SendAsync(message);

            if (response.StatusCode != System.Net.HttpStatusCode.NoContent)
            {
                var body = await response.Content.ReadAsStringAsync();
                _log.Warning("DELETE /events/{Name} returned unexpected status ({Status}): {Body}",
                    characterName, response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Exception during DELETE /events/{Name}.", characterName);
        }
    }

    public async Task<RecruitmentPageResponse?> GetRecruitmentPageAsync(
        string postType,
        int page,
        int pageSize,
        IReadOnlyCollection<string>? gameTypes,
        IReadOnlyCollection<string>? dataCentres,
        bool includeNsfw,
        bool? bank,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = new StringBuilder("recruitment/page?post_type=")
                .Append(Uri.EscapeDataString(postType))
                .Append("&page=").Append(page)
                .Append("&page_size=").Append(pageSize);

            if (includeNsfw)
                url.Append("&include_nsfw=true");

            if (bank.HasValue)
                url.Append("&bank=").Append(bank.Value ? "true" : "false");

            if (gameTypes != null)
                foreach (var g in gameTypes)
                    url.Append("&game_types=").Append(Uri.EscapeDataString(g));

            if (dataCentres != null)
                foreach (var dc in dataCentres)
                    url.Append("&data_centres=").Append(Uri.EscapeDataString(dc));

            var response = await _http.GetAsync(url.ToString(), cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("GET /recruitment/page returned unexpected status ({Status}): {Body}", response.StatusCode, body);
                return null;
            }

            return System.Text.Json.JsonSerializer.Deserialize<RecruitmentPageResponse>(body, JsonOptions);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "GET /recruitment/page exception.");
            return null;
        }
    }

    public async Task<(string? Error, RecruitmentCreateResponse? Created)> PostRecruitmentAsync(PostRecruitmentRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("recruitment", request, JsonOptions);

            if (response.StatusCode == System.Net.HttpStatusCode.RequestEntityTooLarge
                && !string.IsNullOrEmpty(request.ProfilePictureB64))
            {
                _log.Warning("POST /recruitment returned 413; retrying without the picture.");
                request.ProfilePictureB64 = null;
                response = await _http.PostAsJsonAsync("recruitment", request, JsonOptions);
            }

            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("POST /recruitment returned unexpected status ({Status}): {Body}", response.StatusCode, body);
                return (DescribeError(response.StatusCode, body), null);
            }

            var created = System.Text.Json.JsonSerializer.Deserialize<RecruitmentCreateResponse>(body, JsonOptions);
            return (null, created);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Exception during POST /recruitment.");
            return ("Failed to create post. Check the log for details.", null);
        }
    }

    public async Task<(string? Error, RecruitmentPost? Updated)> PutRecruitmentAsync(string id, string sessionToken, PutRecruitmentRequest request)
    {
        try
        {
            var encoded = Uri.EscapeDataString(id);
            using var message = new HttpRequestMessage(HttpMethod.Put, $"recruitment/{encoded}");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);
            message.Content = JsonContent.Create(request, options: JsonOptions);

            var response = await _http.SendAsync(message);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("PUT /recruitment/{Id} returned unexpected status ({Status}): {Body}", id, response.StatusCode, body);
                return (DescribeError(response.StatusCode, body), null);
            }

            var updated = System.Text.Json.JsonSerializer.Deserialize<RecruitmentPost>(body, JsonOptions);
            return (null, updated);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Exception during PUT /recruitment/{Id}.", id);
            return ("Failed to update post. Check the log for details.", null);
        }
    }

    public async Task<bool> DeleteRecruitmentAsync(string id, string sessionToken)
    {
        try
        {
            var encoded = Uri.EscapeDataString(id);
            using var message = new HttpRequestMessage(HttpMethod.Delete, $"recruitment/{encoded}");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);

            var response = await _http.SendAsync(message);

            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                return true;

            var body = await response.Content.ReadAsStringAsync();
            _log.Warning("DELETE /recruitment/{Id} returned unexpected status ({Status}): {Body}", id, response.StatusCode, body);
            return false;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Exception during DELETE /recruitment/{Id}.", id);
            return false;
        }
    }

    private static string DescribeError(System.Net.HttpStatusCode status, string body) => status switch
    {
        System.Net.HttpStatusCode.Conflict => "You already have the maximum number of active posts on this character.",
        System.Net.HttpStatusCode.Forbidden => "Text must not contain URLs or HTML.",
        System.Net.HttpStatusCode.Unauthorized => "You can't edit this post (session token missing or invalid).",
        System.Net.HttpStatusCode.NotFound => "This post no longer exists.",
        System.Net.HttpStatusCode.BadRequest => "Some fields were invalid. Please check your entries.",
        _ => "Request failed. Check the log for details."
    };

    public void Dispose() => _http.Dispose();
}
