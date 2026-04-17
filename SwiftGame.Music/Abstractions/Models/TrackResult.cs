namespace SwiftGame.Music.Abstractions.Models;

public record TrackResult
{
    public required string ProviderId { get; init; }
    public required string Provider { get; init; }
    public required string Title { get; init; }
    public required string Album { get; init; }
    public required string AlbumArt { get; init; }
    public required string ArtistName { get; init; }
    public string? PreviewUrl { get; init; }
    public int DurationMs { get; init; }
    public int ReleaseYear { get; init; }
}