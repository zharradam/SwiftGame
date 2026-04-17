using Microsoft.AspNetCore.Mvc;
using SwiftGame.Data.Entities;
using SwiftGame.Data.Repositories;
using System.Text.Json;

namespace SwiftGame.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SeedController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly ISongRepository _songRepository;

    private const string TaylorSwiftItunesArtistId = "159260351";

    public SeedController(
        IHttpClientFactory httpClientFactory,
        ISongRepository songRepository)
    {
        _httpClient = httpClientFactory.CreateClient();
        _songRepository = songRepository;
    }

    [HttpPost("songs")]
    public async Task<IActionResult> SeedSongs(CancellationToken cancellationToken)
    {
        // Prevent reseeding if catalogue already exists
        var existing = await _songRepository.GetAllAsync(cancellationToken);
        if (existing.Count > 0)
            return BadRequest($"Catalogue already contains {existing.Count} songs. Delete all songs first to reseed.");

        var totalAdded = 0;
        var errors = new List<string>();

        // Step 1 — get all albums for Taylor Swift
        var albumsUrl = $"https://itunes.apple.com/lookup?id={TaylorSwiftItunesArtistId}&entity=album&limit=200";
        var albumsResponse = await _httpClient.GetAsync(albumsUrl, cancellationToken);

        if (!albumsResponse.IsSuccessStatusCode)
            return StatusCode(503, "Failed to fetch albums from iTunes");

        var albumsJson = await albumsResponse.Content.ReadAsStringAsync(cancellationToken);
        var albumsDoc = JsonDocument.Parse(albumsJson);

        var collectionIds = albumsDoc.RootElement
            .GetProperty("results")
            .EnumerateArray()
            .Where(r => r.TryGetProperty("wrapperType", out var w) &&
                        w.GetString() == "collection" &&
                        r.TryGetProperty("artistName", out var a) &&
                        a.GetString()?.Contains("Taylor Swift",
                            StringComparison.OrdinalIgnoreCase) == true)
            .Select(r => r.GetProperty("collectionId").GetInt64())
            .Distinct()
            .ToList();

        Console.WriteLine($"Found {collectionIds.Count} albums");

        // Step 2 — get all tracks per album
        foreach (var collectionId in collectionIds)
        {
            try
            {
                await Task.Delay(300, cancellationToken);

                var tracksUrl = $"https://itunes.apple.com/lookup?id={collectionId}&entity=song&limit=200";
                var tracksResponse = await _httpClient.GetAsync(tracksUrl, cancellationToken);

                if (!tracksResponse.IsSuccessStatusCode) continue;

                var tracksJson = await tracksResponse.Content.ReadAsStringAsync(cancellationToken);
                var tracksDoc = JsonDocument.Parse(tracksJson);

                var songs = tracksDoc.RootElement
                    .GetProperty("results")
                    .EnumerateArray()
                    .Where(t => t.TryGetProperty("wrapperType", out var w) &&
                                w.GetString() == "track" &&
                                t.TryGetProperty("kind", out var k) &&
                                k.GetString() == "song" &&
                                t.TryGetProperty("artistName", out var a) &&
                                a.GetString()?.Contains("Taylor Swift",
                                    StringComparison.OrdinalIgnoreCase) == true &&
                                t.TryGetProperty("previewUrl", out var p) &&
                                p.ValueKind != JsonValueKind.Null)
                    .Select(t => new Song
                    {
                        ProviderId = t.GetProperty("trackId").GetInt64().ToString(),
                        Provider = "iTunes",
                        Title = t.GetProperty("trackName").GetString()!,
                        Album = t.GetProperty("collectionName").GetString()!,
                        AlbumArt = t.GetProperty("artworkUrl100").GetString()!,
                        ArtistName = t.GetProperty("artistName").GetString()!,
                        PreviewUrl = t.GetProperty("previewUrl").GetString(),
                        DurationMs = t.TryGetProperty("trackTimeMillis", out var d)
                                        ? d.GetInt32() : 0,
                        ReleaseYear = int.Parse(
                            t.GetProperty("releaseDate").GetString()!.Split('-')[0])
                    })
                    .ToList();

                Console.WriteLine($"Collection {collectionId}: {songs.Count} songs");

                await _songRepository.UpsertManyAsync(songs, cancellationToken);
                totalAdded += songs.Count;
            }
            catch (Exception ex)
            {
                errors.Add($"Collection {collectionId}: {ex.Message}");
            }
        }

        // Final count
        var finalCount = await _songRepository.GetAllAsync(cancellationToken);

        return Ok(new
        {
            AlbumsFound = collectionIds.Count,
            TotalSeeded = totalAdded,
            TotalInDb = finalCount.Count,
            Errors = errors
        });
    }
}