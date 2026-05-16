using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using GambaWhere.API.Models;

namespace GambaWhere.API;

public class GambaWhereClient : IDisposable
{
    private const string BaseUrl = "https://infernoknights.co.uk";

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
            var response = await _http.GetAsync("/venues");
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

    public async Task<EventResponse[]?> GetEventsAsync()
    {
        try
        {
            var response = await _http.GetAsync("/events");
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("GET /events returned unexpected status ({Status}): {Body}", response.StatusCode, body);
                return null;
            }

            return System.Text.Json.JsonSerializer.Deserialize<EventResponse[]>(body, JsonOptions)
                   ?? Array.Empty<EventResponse>();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "GET /events exception.");
            return null;
        }
    }

    public async Task<EventCreateResponse?> PostEventAsync(PostEventRequest request)
    {
        try
        {
            _log.Information("POST /events payload: {Payload}", System.Text.Json.JsonSerializer.Serialize(request, JsonOptions));
            var response = await _http.PostAsJsonAsync("/events", request, JsonOptions);

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
            using var message = new HttpRequestMessage(HttpMethod.Put, $"/events/{encoded}");
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
            using var message = new HttpRequestMessage(HttpMethod.Delete, $"/events/{encoded}");
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

    public void Dispose() => _http.Dispose();
}
