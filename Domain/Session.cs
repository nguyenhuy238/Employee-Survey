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

        // ====== NEW: tính giờ chủ động (pause/resume) ======
        // Tổng số giây đã tiêu thụ (khi đã "pause" xong mới chốt vào đây)
        public int ConsumedSeconds { get; set; } = 0;
        // Nếu khác null => đang chạy; thời điểm bắt đầu phiên chạy hiện tại
        public DateTime? TimerStartedAt { get; set; }

        // Điểm & snapshot
        public double TotalScore { get; set; } = 0;
        public double MaxScore { get; set; } = 0;
        public double Percent { get; set; } = 0;
        public bool IsPassed { get; set; } = false;

        public List<Answer> Answers { get; set; } = new();
        public List<Question> Snapshot { get; set; } = new();
    }
}
