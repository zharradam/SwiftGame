namespace SwiftGame.Music.Abstractions.Models;

public class MusicProviderException : Exception
{
    public string Provider { get; }
    public int? StatusCode { get; }

    public MusicProviderException(
        string provider,
        string message,
        int? statusCode = null,
        Exception? inner = null)
        : base(message, inner)
    {
        Provider = provider;
        StatusCode = statusCode;
    }
}