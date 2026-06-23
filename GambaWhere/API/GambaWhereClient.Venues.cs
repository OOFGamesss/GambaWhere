using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace GambaWhere.API;

/// <summary>GambaWhereClient venue endpoint: fetches the list of known venue names.</summary>
public partial class GambaWhereClient
{
    public async Task<string[]> GetVenuesAsync()
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, "venues");

            var response = await _http.SendAsync(message);
            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("GET /venues failed: {Status}", response.StatusCode);
                return Array.Empty<string>();
            }

            return await response.Content.ReadFromJsonAsync<string[]>(JsonOptions) ?? Array.Empty<string>();
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "GET /venues failed.");
            return Array.Empty<string>();
        }
    }
}
