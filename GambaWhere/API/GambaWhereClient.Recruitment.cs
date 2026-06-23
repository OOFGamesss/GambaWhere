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

/// <summary>GambaWhereClient recruitment endpoints: paged list, create, update, and delete posts, plus the failure-message mapping.</summary>
public partial class GambaWhereClient
{
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
        var url = new StringBuilder("recruitment/page?post_type=")
            .Append(Uri.EscapeDataString(postType))
            .Append("&page=").Append(page)
            .Append("&page_size=").Append(pageSize);

        if (includeNsfw)
            url.Append("&include_nsfw=true");

        if (bank.HasValue)
            url.Append("&bank=").Append(bank.Value ? "true" : "false");

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
                _log.Warning("GET /recruitment/page failed: {Status}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<RecruitmentPageResponse>(JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "GET /recruitment/page failed.");
            return null;
        }
    }

    public async Task<(string? Error, RecruitmentCreateResponse? Created)> PostRecruitmentAsync(PostRecruitmentRequest request)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "recruitment")
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            };

            var response = await _http.SendAsync(message);
            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("POST /recruitment failed: {Status}", response.StatusCode);
                return (DescribeError(response.StatusCode), null);
            }

            var created = await response.Content.ReadFromJsonAsync<RecruitmentCreateResponse>(JsonOptions);
            return (null, created);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "POST /recruitment failed.");
            return ("Failed to create post. Check the log for details.", null);
        }
    }

    public async Task<(string? Error, RecruitmentPost? Updated)> PutRecruitmentAsync(string id, string sessionToken, PutRecruitmentRequest request)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Put, $"recruitment/{Uri.EscapeDataString(id)}")
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);

            var response = await _http.SendAsync(message);
            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("PUT /recruitment/{Id} failed: {Status}", id, response.StatusCode);
                return (DescribeError(response.StatusCode), null);
            }

            var updated = await response.Content.ReadFromJsonAsync<RecruitmentPost>(JsonOptions);
            return (null, updated);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "PUT /recruitment/{Id} failed.", id);
            return ("Failed to update post. Check the log for details.", null);
        }
    }

    public async Task<bool> DeleteRecruitmentAsync(string id, string sessionToken)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Delete, $"recruitment/{Uri.EscapeDataString(id)}");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);

            var response = await _http.SendAsync(message);
            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("DELETE /recruitment/{Id} failed: {Status}", id, response.StatusCode);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "DELETE /recruitment/{Id} failed.", id);
            return false;
        }
    }

    private static string DescribeError(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Conflict     => "You already have the maximum number of active posts on this character.",
        HttpStatusCode.Forbidden    => "Text must not contain URLs or HTML.",
        HttpStatusCode.Unauthorized => "You can't edit this post (session token missing or invalid).",
        HttpStatusCode.NotFound     => "This post no longer exists.",
        HttpStatusCode.BadRequest   => "Some fields were invalid. Please check your entries.",
        _                           => "Request failed. Check the log for details."
    };
}
