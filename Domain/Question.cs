using System.ComponentModel.DataAnnotations;

namespace Employee_Survey.Domain;

public class Question
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Required, MinLength(5)]
    public string Content { get; set; } = "";

    public QType Type { get; set; } = QType.MCQ;

    public List<string>? Options { get; set; } = new();
    public List<string>? CorrectKeys { get; set; } = new();

    public int? EssayMinWords { get; set; }

    public List<MatchPair>? MatchingPairs { get; set; } = new();

    public DragDropConfig? DragDrop { get; set; }

    [Required] public string Skill { get; set; } = "C#";
    [Required] public string Difficulty { get; set; } = "Junior";
    public List<string> Tags { get; set; } = new();

    public List<MediaFile> Media { get; set; } = new();

    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public record MatchPair(string L, string R);

public class DragDropConfig
{
    public List<string> Tokens { get; set; } = new();
    public List<DragSlot> Slots { get; set; } = new();
}
public record DragSlot(string Name, string Answer);

public class MediaFile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = "";
    public string Url { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public string? Caption { get; set; } // tùy chọn
}
