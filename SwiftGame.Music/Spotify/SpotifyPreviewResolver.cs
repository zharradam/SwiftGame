using SwiftGame.Music.Abstractions;
using SwiftGame.Music.Abstractions.Models;
using SwiftGame.Music.Spotify.Config;
using System.Text.Json;

namespace SwiftGame.Music.Spotify;

public class SpotifyPreviewResolver : IPreviewResolver
{
    private readonly HttpClient _httpClient;
    private readonly SpotifyConfig _config;

    public SpotifyPreviewResolver(HttpClient httpClient, SpotifyConfig config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    public async Task<PreviewClip?> GetPreviewAsync(
        string providerId,
        CancellationToken cancellationToken = default)
    {
        // preview_url is already returned inline from SearchAsync/GetByIdAsync
        // so this resolver is only called when the inline URL was null.
        // We re-fetch the track and check again — Spotify occasionally
        // returns a preview_url on a direct lookup even when search omitted it.

        var url = $"{_config.BaseUrl}/tracks/{providerId}";
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        var previewUrl = doc.RootElement
            .TryGetProperty("preview_url", out var p) && p.ValueKind != JsonValueKind.Null
                ? p.GetString()
                : null;

        return previewUrl is null
            ? null
            : new PreviewClip(previewUrl, 30, "Spotify");
    }
}