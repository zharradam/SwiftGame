using System.Text.RegularExpressions;

namespace SwiftGame.API.Services;

// Server copy of the client's era map (SwiftGame.Client/src/app/components/game/eras.ts). Keep the two in step.
public static class Eras
{
    public record Era(string Key, string Label);

    private static readonly (Regex Match, Era Era)[] Map =
    [
        (new(@"tortured poets", RegexOptions.IgnoreCase), new("ttpd", "The Tortured Poets Department")),
        (new(@"showgirl", RegexOptions.IgnoreCase), new("showgirl", "The Life of a Showgirl")),
        (new(@"midnights", RegexOptions.IgnoreCase), new("midnights", "Midnights")),
        (new(@"evermore", RegexOptions.IgnoreCase), new("evermore", "evermore")),
        (new(@"folklore", RegexOptions.IgnoreCase), new("folklore", "folklore")),
        (new(@"lover", RegexOptions.IgnoreCase), new("lover", "Lover")),
        (new(@"reputation", RegexOptions.IgnoreCase), new("rep", "reputation")),
        (new(@"1989"), new("1989", "1989")),
        (new(@"^red\b", RegexOptions.IgnoreCase), new("red", "Red")),
        (new(@"speak now", RegexOptions.IgnoreCase), new("speaknow", "Speak Now")),
        (new(@"fearless", RegexOptions.IgnoreCase), new("fearless", "Fearless")),
        (new(@"^taylor swift", RegexOptions.IgnoreCase), new("debut", "Taylor Swift")),
    ];

    public static IReadOnlyList<Era> All { get; } = Map.Select(m => m.Era).ToList();

    public static Era? Of(string? album)
        => string.IsNullOrWhiteSpace(album) ? null : Map.FirstOrDefault(m => m.Match.IsMatch(album)).Era;

    public static bool IsKey(string? key) => All.Any(e => e.Key == key);
}

// Reports and daily sets follow the owner's local day
public static class Clock
{
    public static readonly TimeZoneInfo Zone =
        TimeZoneInfo.TryFindSystemTimeZoneById("Australia/Adelaide", out var tz) ? tz
        : TimeZoneInfo.TryFindSystemTimeZoneById("Cen. Australia Standard Time", out tz) ? tz
        : TimeZoneInfo.Utc;

    public static DateTime Local(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    public static DateTime Today => Local(DateTime.UtcNow).Date;

    // UTC instant of local midnight today
    public static DateTime TodayStartUtc => TimeZoneInfo.ConvertTimeToUtc(Today, Zone);
}
