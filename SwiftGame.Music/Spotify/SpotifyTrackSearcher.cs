using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SwiftGame.Music.Abstractions;
using SwiftGame.Music.Abstractions.Models;
using SwiftGame.Music.Spotify.Config;

namespace SwiftGame.Music.Spotify;

public class SpotifyTrackSearcher : ITrackSearcher
{
    private readonly HttpClient _httpClient;
    private readonly SpotifyConfig _config;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public SpotifyTrackSearcher(HttpClient httpClient, SpotifyConfig config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    public async Task<IReadOnlyList<TrackResult>> SearchAsync(
    string query,
    int limit = 10,
    CancellationToken cancellationToken = default)
    {
        await EnsureTokenAsync(cancellationToken);

        var encodedQuery = Uri.EscapeDataString("artist:Taylor Swift");
        var url = $"{_config.BaseUrl}/search?q={encodedQuery}&type=track&limit=10";

        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new MusicProviderException(
                "Spotify",
                $"Search failed with status {response.StatusCode}",
                (int)response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(json);

        return doc.RootElement
            .GetProperty("tracks")
            .GetProperty("items")
            .EnumerateArray()
            .Where(t => t.GetProperty("artists")
                         .EnumerateArray()
                         .Any(a => a.GetProperty("name")
                                    .GetString()
                                    ?.Contains("Taylor Swift",
                                        StringComparison.OrdinalIgnoreCase) == true))
            .Select(MapToTrackResult)
            .ToList();
    }

    public async Task<TrackResult?> GetByIdAsync(
        string providerId,
        CancellationToken cancellationToken = default)
    {
        await EnsureTokenAsync(cancellationToken);

        var url = $"{_config.BaseUrl}/tracks/{providerId}";
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
            throw new MusicProviderException(
                "Spotify",
                $"GetById failed with status {response.StatusCode}",
                (int)response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(json);

        return MapToTrackResult(doc.RootElement);
    }

    public async Task<IReadOnlyList<TrackResult>> GetByIdsAsync(
        IEnumerable<string> providerIds,
        CancellationToken cancellationToken = default)
    {
        await EnsureTokenAsync(cancellationToken);

        var ids = string.Join(",", providerIds);
        var url = $"{_config.BaseUrl}/tracks?ids={ids}";
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new MusicProviderException(
                "Spotify",
                $"GetByIds failed with status {response.StatusCode}",
                (int)response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(json);

        return doc.RootElement
            .GetProperty("tracks")
            .EnumerateArray()
            .Select(MapToTrackResult)
            .ToList();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task EnsureTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null && DateTime.UtcNow < _tokenExpiry)
            return;

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_config.ClientId}:{_config.ClientSecret}"));

        var request = new HttpRequestMessage(HttpMethod.Post, _config.AccountsUrl)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Basic", credentials) },
            Content = new FormUrlEncodedContent(new[]
            {
            new KeyValuePair<string, string>("grant_type", "client_credentials")
        })
        };

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new MusicProviderException(
                "Spotify",
                "Failed to obtain access token",
                (int)response.StatusCode);

        var token = JsonSerializer.Deserialize<SpotifyTokenResponse>(json)!;

        _accessToken = token.AccessToken;
        _tokenExpiry = DateTime.UtcNow.AddSeconds(token.ExpiresIn - 60);

        // Attach bearer token for all subsequent requests
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    private static TrackResult MapToTrackResult(JsonElement track)
    {
        var album = track.GetProperty("album");
        var artists = track.GetProperty("artists");

        var previewUrl = track.TryGetProperty("preview_url", out var p) && p.ValueKind != JsonValueKind.Null
                            ? p.GetString()
                            : null;

        return new TrackResult
        {
            ProviderId = track.GetProperty("id").GetString()!,
            Provider = "Spotify",
            Title = track.GetProperty("name").GetString()!,
            Album = album.GetProperty("name").GetString()!,
            AlbumArt = album.GetProperty("images")[0].GetProperty("url").GetString()!,
            ArtistName = artists[0].GetProperty("name").GetString()!,
            PreviewUrl = previewUrl,
            DurationMs = track.GetProperty("duration_ms").GetInt32(),
            ReleaseYear = int.Parse(
                album.GetProperty("release_date").GetString()!.Split('-')[0])
        };
    }
}