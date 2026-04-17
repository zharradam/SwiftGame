using SwiftGame.Music.Abstractions;
using SwiftGame.Music.Abstractions.Models;
using SwiftGame.Music.iTunes.Config;
using System.Text.Json;

namespace SwiftGame.Music.iTunes;

public class ItunesPreviewResolver : IPreviewResolver
{
    private readonly HttpClient _httpClient;
    private readonly ItunesConfig _config;

    public ItunesPreviewResolver(HttpClient httpClient, ItunesConfig config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    public async Task<PreviewClip?> GetPreviewAsync(
        string providerId,
        CancellationToken cancellationToken = default)
    {
        // First try direct iTunes lookup by ID
        var url = $"{_config.BaseUrl}/lookup?id={providerId}";
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);
            var results = doc.RootElement
                .GetProperty("results")
                .EnumerateArray()
                .ToList();

            if (results.Count > 0)
            {
                var previewUrl = results[0].TryGetProperty("previewUrl", out var p)
                    ? p.GetString()
                    : null;

                if (previewUrl is not null)
                    return new PreviewClip(previewUrl, 30, "iTunes");
            }
        }

        // iTunes ID lookup failed (Spotify ID won't match iTunes ID)
        // Fall back to searching by Taylor Swift to get any track with a preview
        var searchUrl = $"{_config.BaseUrl}/search?term=Taylor+Swift&entity=song&limit=50";
        var searchResponse = await _httpClient.GetAsync(searchUrl, cancellationToken);

        if (!searchResponse.IsSuccessStatusCode)
            return null;

        var searchJson = await searchResponse.Content.ReadAsStringAsync(cancellationToken);
        var searchDoc = JsonDocument.Parse(searchJson);

        var track = searchDoc.RootElement
            .GetProperty("results")
            .EnumerateArray()
            .FirstOrDefault(t => t.TryGetProperty("previewUrl", out var p)
                              && p.GetString() is not null);

        if (track.ValueKind == JsonValueKind.Undefined)
            return null;

        var url2 = track.GetProperty("previewUrl").GetString()!;
        return new PreviewClip(url2, 30, "iTunes");
    }
}