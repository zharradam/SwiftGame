namespace SwiftGame.Music.Spotify.Config;

public class SpotifyConfig
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.spotify.com/v1";
    public string AccountsUrl { get; set; } = "https://accounts.spotify.com/api/token";
}