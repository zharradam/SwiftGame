using SwiftGame.Music.Abstractions.Models;

namespace SwiftGame.Music.Abstractions;

public interface IPreviewResolver
{
    Task<PreviewClip?> GetPreviewAsync(
        string providerId,
        CancellationToken cancellationToken = default);
}