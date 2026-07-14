using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using GambaWhere.Models;

namespace GambaWhere.API;

/// <summary>GambaWhereClient game store endpoint: list the approved games.</summary>
public partial class GambaWhereClient
{
    public async Task<GameStoreGame[]?> GetGamesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, "games");

            var response = await _http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("GET /games failed: {Status}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GameStoreGame[]>(JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
                _log.Warning(ex, "GET /games failed.");
            return null;
        }
    }
}
