using SwiftGame.Music.Abstractions;

namespace SwiftGame.Music.Fallback;

public class FallbackMusicFactory : IMusicProviderFactory
{
    private readonly IMusicProviderFactory _primary;
    private readonly IMusicProviderFactory _fallback;

    public FallbackMusicFactory(
        IMusicProviderFactory primary,
        IMusicProviderFactory fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public string ProviderName
        => $"{_primary.ProviderName}→{_fallback.ProviderName}";

    public ITrackSearcher CreateTrackSearcher()
        => _primary.CreateTrackSearcher();

    public IPreviewResolver CreatePreviewResolver()
        => new FallbackPreviewResolver(
            _primary.CreatePreviewResolver(),
            _fallback.CreatePreviewResolver()
        );
}