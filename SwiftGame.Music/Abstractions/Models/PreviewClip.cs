namespace SwiftGame.Music.Abstractions.Models;

public record PreviewClip(
    string Url,
    int DurationSeconds,
    string Provider
);