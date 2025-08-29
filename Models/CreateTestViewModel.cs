using System.Collections.Generic;
using Employee_Survey.Application;
using Employee_Survey.Domain;

namespace Employee_Survey.Models
{
    public class CreateTestViewModel
    {
        public PagedResult<Question> Page { get; set; } = new();
        public QuestionFilter Filter { get; set; } = new();

        public string Title { get; set; } = "";
        public int DurationMinutes { get; set; } = 10;
        public int PassScore { get; set; } = 3;
        public string SkillFilter { get; set; } = "ASP.NET";
        public int RandomMCQ { get; set; } = 2;
        public int RandomTF { get; set; } = 1;
        public int RandomEssay { get; set; } = 0;

        public List<string> SelectedQuestionIds { get; set; } = new();

        public List<Question> MCQQuestions => Page.Items?.FindAll(q => q.Type == QType.MCQ) ?? new();
        public List<Question> TFQuestions => Page.Items?.FindAll(q => q.Type == QType.TrueFalse) ?? new();
        public List<Question> EssayQuestions => Page.Items?.FindAll(q => q.Type == QType.Essay) ?? new();
        public List<Question> MatchingQuestions => Page.Items?.FindAll(q => q.Type == QType.Matching) ?? new();
        public List<Question> DragDropQuestions => Page.Items?.FindAll(q => q.Type == QType.DragDrop) ?? new();
    }
}
