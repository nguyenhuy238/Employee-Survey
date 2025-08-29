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

        public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

        // Timer (pause/resume)
        public int ConsumedSeconds { get; set; } = 0;
        public DateTime? TimerStartedAt { get; set; }  // ✅ chỉ set ở /resume

        // Scoring
        public double AutoScore { get; set; } = 0;
        public double ManualScore { get; set; } = 0;
        public double TotalScore { get; set; } = 0;
        public double MaxScore { get; set; } = 0;
        public double Percent { get; set; } = 0;
        public bool IsPassed { get; set; } = false;

        // Payload
        public List<Answer> Answers { get; set; } = new();
        public List<Question> Snapshot { get; set; } = new();

        // Points per question (frozen)
        public List<SessionItem> Items { get; set; } = new();
    }

    public class SessionItem
    {
        public string QuestionId { get; set; } = "";
        public decimal Points { get; set; } = 1m;
    }
}
