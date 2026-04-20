namespace SwiftGame.Data.Entities;

public class Album
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool IsIncluded { get; set; } = true;

    // Navigation
    public ICollection<Song> Songs { get; set; } = [];
}