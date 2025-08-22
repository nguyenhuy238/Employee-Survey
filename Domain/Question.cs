using System.ComponentModel.DataAnnotations;

namespace Employee_Survey.Domain;

public class Question
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Required, MinLength(5)]
    public string Content { get; set; } = "";

    public QType Type { get; set; } = QType.MCQ;

    // MCQ/TrueFalse -> Options + CorrectKeys (nhiều đáp án đúng cho MCQ multiple)
    public List<string>? Options { get; set; } = new();           // ["A","B","C","D"]
    public List<string>? CorrectKeys { get; set; } = new();        // ["A"] hoặc ["True"]

    // Essay -> không dùng Options
    public int? EssayMinWords { get; set; }

    // Matching -> Danh sách cặp (Left-Right)
    public List<MatchPair>? MatchingPairs { get; set; } = new();   // [{"L":"HTTP","R":"Protocol"}]

    // DragDrop -> các “slot” & “tokens”
    public DragDropConfig? DragDrop { get; set; }

    // Phân loại
    [Required] public string Skill { get; set; } = "C#";
    [Required] public string Difficulty { get; set; } = "Junior";
    public List<string> Tags { get; set; } = new();

    // Media
    public List<MediaFile> Media { get; set; } = new();

    // Audit
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public record MatchPair(string L, string R);

public class DragDropConfig
{
    public List<string> Tokens { get; set; } = new(); // ["IIS","Kestrel","Controller"]
    public List<DragSlot> Slots { get; set; } = new(); // [{"Name":"WebServer","Answer":"IIS"}]
}
public record DragSlot(string Name, string Answer);

public class MediaFile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = "";
    public string Url { get; set; } = "";  // /uploads/xxx.png
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
}
