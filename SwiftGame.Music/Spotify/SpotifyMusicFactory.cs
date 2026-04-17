using Microsoft.Extensions.Options;
using SwiftGame.Music.Abstractions;
using SwiftGame.Music.Spotify.Config;

namespace SwiftGame.Music.Spotify;

public class SpotifyMusicFactory : IMusicProviderFactory
{
    private readonly SpotifyConfig _config;
    private readonly HttpClient _httpClient;

    public SpotifyMusicFactory(
        HttpClient httpClient,
        IOptions<SpotifyConfig> config)
    {
        _httpClient = httpClient;
        _config = config.Value;
    }

    public string ProviderName => "Spotify";

    public ITrackSearcher CreateTrackSearcher()
        => new SpotifyTrackSearcher(_httpClient, _config);

    public IPreviewResolver CreatePreviewResolver()
        => new SpotifyPreviewResolver(_httpClient, _config);
}