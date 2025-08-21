namespace Employee_Survey.Domain
{
    public class Assignment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string TestId { get; set; } = "";
        
        public string TargetType { get; set; } = "Role";
        public string TargetValue { get; set; } = "Employee";
        public DateTime StartAt { get; set; } = DateTime.UtcNow.AddMinutes(-30);
        public DateTime EndAt { get; set; } = DateTime.UtcNow.AddDays(7);
    }
}
