// File: Controllers/CreateTestViewModel.cs
using System.Collections.Generic;
using Employee_Survey.Domain;

namespace Employee_Survey.Models
{
    public class CreateTestViewModel
    {
        // Danh sách câu hỏi để hiển thị
        public List<Question> MCQQuestions { get; set; } = new();
        public List<Question> TFQuestions { get; set; } = new();
        public List<Question> EssayQuestions { get; set; } = new();

        // Các field cấu hình Test (để bind lại khi post lỗi)
        public string Title { get; set; } = "";
        public int DurationMinutes { get; set; } = 10;
        public int PassScore { get; set; } = 3;
        public string SkillFilter { get; set; } = "ASP.NET";
        public int RandomMCQ { get; set; } = 2;
        public int RandomTF { get; set; } = 1;
        public int RandomEssay { get; set; } = 0;

        // Danh sách Id câu hỏi được chọn (checkbox)
        public List<string> SelectedQuestionIds { get; set; } = new();
    }
}
