using SwiftGame.Music.Abstractions;
using SwiftGame.Music.Abstractions.Models;

namespace SwiftGame.Music.Fallback;

public class FallbackPreviewResolver : IPreviewResolver
{
    private readonly IPreviewResolver _primary;
    private readonly IPreviewResolver _fallback;

    public FallbackPreviewResolver(
        IPreviewResolver primary,
        IPreviewResolver fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public async Task<PreviewClip?> GetPreviewAsync(
        string providerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var clip = await _primary.GetPreviewAsync(providerId, cancellationToken);
            if (clip is not null) return clip;
        }
        catch (MusicProviderException)
        {
            // Primary provider failed entirely — fall through to fallback
        }

        return await _fallback.GetPreviewAsync(providerId, cancellationToken);
    }
}