namespace Employee_Survey.Domain
{
    public class Session
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string TestId { get; set; } = "";
        public string UserId { get; set; } = "";
        public DateTime StartAt { get; set; } = DateTime.UtcNow;
        public DateTime? EndAt { get; set; }
        public SessionStatus Status { get; set; } = SessionStatus.Draft;
        public double TotalScore { get; set; } = 0;
        public List<Answer> Answers { get; set; } = new();
        public List<Question> Snapshot { get; set; } = new();
    }
}
