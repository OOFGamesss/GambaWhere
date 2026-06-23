using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Dalamud.Plugin.Services;

namespace GambaWhere.API;

/// <summary>HTTP client for the GambaWhere API</summary>
public partial class GambaWhereClient : IDisposable
{
    private const string BaseUrl = "https://api.oofgames.fyi/v1/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly IPluginLog _log;

    public GambaWhereClient(IPluginLog log)
    {
        _log  = log;
        _http = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public void Dispose() => _http.Dispose();
}
