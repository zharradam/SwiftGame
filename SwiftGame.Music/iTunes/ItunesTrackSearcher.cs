using System.Text.Json;
using SwiftGame.Music.Abstractions;
using SwiftGame.Music.Abstractions.Models;
using SwiftGame.Music.iTunes.Config;

namespace SwiftGame.Music.iTunes;

public class ItunesTrackSearcher : ITrackSearcher
{
    private readonly HttpClient _httpClient;
    private readonly ItunesConfig _config;

    public ItunesTrackSearcher(HttpClient httpClient, ItunesConfig config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    public async Task<IReadOnlyList<TrackResult>> SearchAsync(
    string query,
    int limit = 10,
    CancellationToken cancellationToken = default)
    {
        var searchTerms = new[]
        {
            "Taylor Swift Taylor Swift",
            "Taylor Swift Fearless",
            "Taylor Swift Fearless Taylors Version",
            "Taylor Swift Speak Now",
            "Taylor Swift Speak Now Taylors Version",
            "Taylor Swift Red",
            "Taylor Swift Red Taylors Version",
            "Taylor Swift 1989",
            "Taylor Swift 1989 Taylors Version",
            "Taylor Swift Reputation",
            "Taylor Swift Lover",
            "Taylor Swift Folklore",
            "Taylor Swift Evermore",
            "Taylor Swift Midnights",
            "Taylor Swift Tortured Poets Department",
            "Taylor Swift Life of a Showgirl"
        };

        // Fetch from all albums in parallel
        var tasks = searchTerms.Select(term =>
        {
            var url = $"{_config.BaseUrl}/search?term={Uri.EscapeDataString(term)}&entity=song&limit=10&attribute=albumTerm";
            return _httpClient.GetAsync(url, cancellationToken);
        });

        var responses = await Task.WhenAll(tasks);

        var allTracks = new List<TrackResult>();

        foreach (var response in responses)
        {
            if (!response.IsSuccessStatusCode) continue;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);

            var tracks = doc.RootElement
                .GetProperty("results")
                .EnumerateArray()
                .Where(t => t.TryGetProperty("artistName", out var a) &&
                            a.GetString()?.Contains("Taylor Swift",
                                StringComparison.OrdinalIgnoreCase) == true &&
                            t.TryGetProperty("previewUrl", out var p) &&
                            p.ValueKind != JsonValueKind.Null)
                .Select(MapToTrackResult)
                .ToList();

            allTracks.AddRange(tracks);
        }

        if (allTracks.Count == 0)
            throw new MusicProviderException(
                "iTunes",
                "No tracks found across any album",
                null);

        // Deduplicate by title and shuffle
        return allTracks
            .GroupBy(t => t.Title)
            .Select(g => g.First())
            .OrderBy(_ => Random.Shared.Next())
            .Take(limit)
            .ToList();
    }

    public async Task<TrackResult?> GetByIdAsync(
        string providerId,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_config.BaseUrl}/lookup?id={providerId}";
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(json);
        var results = doc.RootElement.GetProperty("results").EnumerateArray().ToList();

        return results.Count == 0 ? null : MapToTrackResult(results[0]);
    }

    public async Task<IReadOnlyList<TrackResult>> GetByIdsAsync(
        IEnumerable<string> providerIds,
        CancellationToken cancellationToken = default)
    {
        // iTunes lookup supports comma-separated IDs in one call
        var ids = string.Join(",", providerIds);
        var url = $"{_config.BaseUrl}/lookup?id={ids}";
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new MusicProviderException(
                "iTunes",
                $"GetByIds failed with status {response.StatusCode}",
                (int)response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(json);

        return doc.RootElement
            .GetProperty("results")
            .EnumerateArray()
            .Select(MapToTrackResult)
            .ToList();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static TrackResult MapToTrackResult(JsonElement track)
    {
        var releaseDateStr = track.GetProperty("releaseDate").GetString() ?? "2000-01-01";
        var releaseYear = int.Parse(releaseDateStr.Split('-')[0]);

        return new TrackResult
        {
            ProviderId = track.GetProperty("trackId").GetInt64().ToString(),
            Provider = "iTunes",
            Title = track.GetProperty("trackName").GetString()!,
            Album = track.GetProperty("collectionName").GetString()!,
            AlbumArt = track.GetProperty("artworkUrl100").GetString()!,
            ArtistName = track.GetProperty("artistName").GetString()!,
            PreviewUrl = track.TryGetProperty("previewUrl", out var p)
                            ? p.GetString()
                            : null,
            DurationMs = track.GetProperty("trackTimeMillis").GetInt32(),
            ReleaseYear = releaseYear
        };
    }
}