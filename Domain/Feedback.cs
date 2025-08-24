// Employee_Survey.Domain/Feedback.cs
namespace Employee_Survey.Domain
{
    public class Feedback
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string SessionId { get; set; } = "";
        public string Content { get; set; } = "";
        public int Rating { get; set; } = 5;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
