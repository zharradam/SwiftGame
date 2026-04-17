namespace SwiftGame.Music.Abstractions;

public interface IMusicProviderFactory
{
    string ProviderName { get; }
    ITrackSearcher CreateTrackSearcher();
    IPreviewResolver CreatePreviewResolver();
}