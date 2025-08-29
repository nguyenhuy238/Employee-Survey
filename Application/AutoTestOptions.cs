using System;
using System.Collections.Generic;

namespace Employee_Survey.Application
{
    /// <summary>
    /// Tham số cho Auto Generate (khớp View AutoTests/Generate.cshtml).
    /// </summary>
    public class AutoTestOptions
    {
        // Target
        public string Mode { get; set; } = "Department"; // "Department" | "Users"
        public string? Department { get; set; }
        public List<string>? UserIds { get; set; }

        // Filters
        public List<string>? Skills { get; set; } // nhiều input hidden name="Skills" (đã có JS trong View)
        public string DifficultyPolicy { get; set; } = "ByLevel"; // "ByLevel" | "Any"

        // Per-type counts
        public int McqCount { get; set; } = 8;
        public int TfCount { get; set; } = 2;
        public int MatchingCount { get; set; } = 0;
        public int DragDropCount { get; set; } = 0;
        public int EssayCount { get; set; } = 0;

        // Scoring
        public decimal TotalScore { get; set; } = 10m;
        /// <summary>Điểm dành riêng cho Essay (0 => allocator tự mặc định 2.0 khi có Essay).</summary>
        public decimal EssayReserved { get; set; } = 0m;

        // Chính sách khi thiếu câu
        /// <summary>
        /// true => nếu không đủ câu sau khi đã nới lỏng tối đa, ném lỗi để UI báo rõ (khuyến nghị).
        /// false => chấp nhận tạo ít hơn (hành vi cũ, KHÔNG khuyến nghị).
        /// </summary>
        public bool FailWhenInsufficient { get; set; } = true;

        // Assign window (dùng cho Assign All)
        public DateTime? StartAtUtc { get; set; }
        public DateTime? EndAtUtc { get; set; }
    }
}
