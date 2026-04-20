namespace SwiftGame.Data.Entities;

public class Song
{
    public Guid Id { get; set; }
    public string ProviderId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;  // kept as display name
    public string AlbumArt { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string? PreviewUrl { get; set; }
    public int DurationMs { get; set; }
    public int ReleaseYear { get; set; }

    // FK to Album table — nullable so existing songs aren't broken before migration
    public Guid? AlbumId { get; set; }
    public Album? AlbumRef { get; set; }  // navigation property (named AlbumRef to avoid clash with Album string)

    public ICollection<Score> Scores { get; set; } = new List<Score>();
}