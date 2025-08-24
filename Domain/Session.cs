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

        // Điểm & thống kê
        public double TotalScore { get; set; } = 0;     // tổng điểm đạt được
        public double MaxScore { get; set; } = 0;       // tổng điểm tối đa (auto-graded)
        public double Percent { get; set; } = 0;        // %
        public bool IsPassed { get; set; } = false;     // đạt/không đạt theo PassScore của Test

        public List<Answer> Answers { get; set; } = new();
        public List<Question> Snapshot { get; set; } = new();
    }
}
