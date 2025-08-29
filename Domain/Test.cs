using System;

namespace Employee_Survey.Domain
{
    public class Test
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "";

        // Cấu hình chung
        public int DurationMinutes { get; set; } = 30;

        /// <summary>
        /// Ngưỡng đậu tính theo "điểm" (không phải số câu).
        /// Mặc định 5/10 (tương đương 50%).
        /// </summary>
        public int PassScore { get; set; } = 5;

        public bool ShuffleQuestions { get; set; } = true;

        // Cấu hình random cũ (giữ nguyên để tương thích)
        public string SkillFilter { get; set; } = "C#";
        public int RandomMCQ { get; set; } = 5;
        public int RandomTF { get; set; } = 5;
        public int RandomEssay { get; set; } = 0;

        // === NEW: Tổng điểm đề (mặc định 10.0) ===
        public decimal TotalMaxScore { get; set; } = 10m;

        // === NEW: Danh sách câu hỏi kèm "điểm" (ưu tiên dùng nếu có) ===
        public List<TestItem> Items { get; set; } = new();

        // Trạng thái publish & danh sách Id câu hỏi (cũ) - vẫn giữ để tương thích
        public bool IsPublished { get; set; } = false;
        public List<string> QuestionIds { get; set; } = new();

        public FrozenRandomConfig? FrozenRandom { get; set; }

        // Audit
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
    }

    public class FrozenRandomConfig
    {
        public string SkillFilter { get; set; } = "C#";
        public int RandomMCQ { get; set; }
        public int RandomTF { get; set; }
        public int RandomEssay { get; set; }
    }

    /// <summary>
    /// Câu hỏi nằm trong Test cùng số điểm của nó.
    /// </summary>
    public class TestItem
    {
        public string QuestionId { get; set; } = "";
        public decimal Points { get; set; } = 1m;
    }
}
