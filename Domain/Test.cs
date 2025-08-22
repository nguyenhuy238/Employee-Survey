// File: Domain/Test.cs
namespace Employee_Survey.Domain
{
    public class Test
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "";

        // Cấu hình chung
        public int DurationMinutes { get; set; } = 30;
        public int PassScore { get; set; } = 5;
        public bool ShuffleQuestions { get; set; } = true;

        // Cấu hình lọc (giữ nguyên field cũ để tương thích)
        public string SkillFilter { get; set; } = "C#";
        public int RandomMCQ { get; set; } = 5;
        public int RandomTF { get; set; } = 5;
        public int RandomEssay { get; set; } = 0;

        // --- NEW: Trạng thái publish & danh sách câu hỏi đã khóa ---
        public bool IsPublished { get; set; } = false;

        /// <summary>
        /// Danh sách Id câu hỏi (khi chọn thủ công hoặc sau khi Publish “đóng băng”
        /// kết quả random). Dùng để kiểm tra một Question có đang nằm trong đề Published hay không.
        /// </summary>
        public List<string> QuestionIds { get; set; } = new();

        /// <summary>
        /// Ảnh chụp cấu hình random tại thời điểm Publish (để audit).
        /// Không dùng để hiển thị đề, chỉ để trace.
        /// </summary>
        public FrozenRandomConfig? FrozenRandom { get; set; }

        // Audit nhẹ
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
    }

    /// <summary>
    /// Lưu lại cấu hình random đã dùng khi publish (để sau này biết vì sao đề có 12 MCQ, 3 TF,...)
    /// </summary>
    public class FrozenRandomConfig
    {
        public string SkillFilter { get; set; } = "C#";
        public int RandomMCQ { get; set; }
        public int RandomTF { get; set; }
        public int RandomEssay { get; set; }
    }
}




//namespace Employee_Survey.Domain
//{
//    public class Test
//    {
//        public string Id { get; set; } = Guid.NewGuid().ToString("N");
//        public string Title { get; set; } = "";
//        public int DurationMinutes { get; set; } = 30;
//        public int PassScore { get; set; } = 5;
//        public string SkillFilter { get; set; } = "C#";
//        public int RandomMCQ { get; set; } = 5;
//        public int RandomTF { get; set; } = 5;
//        public int RandomEssay { get; set; } = 0;
//    }
//}
