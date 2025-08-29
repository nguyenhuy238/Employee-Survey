using System.Collections.Generic;
using Employee_Survey.Application;
using Employee_Survey.Domain;

namespace Employee_Survey.Models
{
    public class EditTestViewModel
    {
        // ==== Core info ====
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public int DurationMinutes { get; set; }
        public int PassScore { get; set; }
        public bool ShuffleQuestions { get; set; }
        public string SkillFilter { get; set; } = "";
        public int RandomMCQ { get; set; }
        public int RandomTF { get; set; }
        public int RandomEssay { get; set; }
        public bool IsPublished { get; set; }

        // ==== Paging + Filter ====
        public PagedResult<Question> Page { get; set; } = new();
        public QuestionFilter Filter { get; set; } = new();

        // ==== Manual selection ====
        public List<string> SelectedQuestionIds { get; set; } = new();

        // ==== 5 groups on current page ====
        public List<Question> MCQQuestions => Page.Items?.FindAll(q => q.Type == QType.MCQ) ?? new();
        public List<Question> TFQuestions => Page.Items?.FindAll(q => q.Type == QType.TrueFalse) ?? new();
        public List<Question> EssayQuestions => Page.Items?.FindAll(q => q.Type == QType.Essay) ?? new();
        public List<Question> MatchingQuestions => Page.Items?.FindAll(q => q.Type == QType.Matching) ?? new();
        public List<Question> DragDropQuestions => Page.Items?.FindAll(q => q.Type == QType.DragDrop) ?? new();
    }
}
