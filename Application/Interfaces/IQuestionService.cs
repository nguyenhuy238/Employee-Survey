using Employee_Survey.Domain;

namespace Employee_Survey.Application
{
    public class QuestionFilter
    {
        public string? Keyword { get; set; }
        public QType? Type { get; set; }
        public string? Skill { get; set; }
        public string? Difficulty { get; set; }
        public string? TagsCsv { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? Sort { get; set; } = "CreatedAt_desc";
    }

    public class PagedResult<T>
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public List<T> Items { get; set; } = new();
    }

    public interface IQuestionService
    {
        Task<PagedResult<Question>> SearchAsync(QuestionFilter f);
        Task<Question?> GetAsync(string id);
        Task<string> CreateAsync(Question q, string actor);
        Task<(bool Success, string? Reason)> UpdateAsync(Question q, string actor);
        Task<(bool Success, string? Reason)> DeleteAsync(string id, string actor);
        Task<string> CloneAsync(string id, string actor);
    }

    

    
}
