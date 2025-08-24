// File: Models/EditTestViewModel.cs
using System.Collections.Generic;
using Employee_Survey.Domain;

namespace Employee_Survey.Models
{
    public class EditTestViewModel
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public int DurationMinutes { get; set; } = 30;
        public int PassScore { get; set; } = 5;
        public bool ShuffleQuestions { get; set; } = true;

        public string SkillFilter { get; set; } = "C#";
        public int RandomMCQ { get; set; } = 5;
        public int RandomTF { get; set; } = 5;
        public int RandomEssay { get; set; } = 0;

        public bool IsPublished { get; set; } = false;

        // Hiển thị & lựa chọn câu hỏi
        public List<Question> MCQQuestions { get; set; } = new();
        public List<Question> TFQuestions { get; set; } = new();
        public List<Question> EssayQuestions { get; set; } = new();

        public List<string> SelectedQuestionIds { get; set; } = new();
    }
}
