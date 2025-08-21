namespace Employee_Survey.Domain
{
    public class Question
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public QType Type { get; set; } = QType.MCQ;
        public string Content { get; set; } = "";
        public List<string>? Options { get; set; }
        public string? Correct { get; set; }      
        public string Skill { get; set; } = "C#";
        public string Difficulty { get; set; } = "Junior";
        public List<string> Tags { get; set; } = new();

    }
}
