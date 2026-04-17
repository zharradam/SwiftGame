using SwiftGame.Music.Abstractions;
using SwiftGame.Music.iTunes.Config;

namespace SwiftGame.Music.iTunes;

public class ItunesMusicFactory : IMusicProviderFactory
{
    private readonly HttpClient _httpClient;
    private readonly ItunesConfig _config;

    public ItunesMusicFactory(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _config = new ItunesConfig();
    }

    public string ProviderName => "iTunes";

    public ITrackSearcher CreateTrackSearcher()
        => new ItunesTrackSearcher(_httpClient, _config);

    public IPreviewResolver CreatePreviewResolver()
        => new ItunesPreviewResolver(_httpClient, _config);
}