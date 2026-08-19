using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using GambaWhere.Models;

namespace GambaWhere.API;

/// <summary>GambaWhereClient game types endpoint: list category metadata.</summary>
public partial class GambaWhereClient
{
    public async Task<GameTypeDto[]?> GetGameTypesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, "games/types");

            var response = await _http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("GET /games/types failed: {Status}", response.StatusCode);
                return null;
            }

            var types = await response.Content.ReadFromJsonAsync<GameTypeDto[]>(JsonOptions, cancellationToken);
            return types?
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
                _log.Warning(ex, "GET /games/types failed.");
            return null;
        }
    }
}
