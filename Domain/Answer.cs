namespace Employee_Survey.Domain
{
    public class Answer
    {
        public string QuestionId { get; set; } = "";
        public string? Selected { get; set; }
        public double Score { get; set; }
        public string? TextAnswer { get; set; }
    }
}
