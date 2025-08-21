namespace Employee_Survey.Models
{
    public class HrDashboardViewModel
    {
        // Cards
        public int TotalEmployees { get; set; }
        public int TotalTests { get; set; }
        public int ActiveAssignments { get; set; }
        public double PassRatePercent { get; set; }

        // Tables
        public List<ActiveAssignmentRow> ActiveAssignmentsList { get; set; } = new();
        public List<RecentSubmissionRow> RecentSubmissions { get; set; } = new();

        // Charts (đơn giản: top skill theo số câu đã làm)
        public List<SkillStat> TopSkills { get; set; } = new();

        public class ActiveAssignmentRow
        {
            public string TestId { get; set; } = "";
            public string TestTitle { get; set; } = "";
            public string Target { get; set; } = ""; // Role/Team/User
            public DateTime StartAt { get; set; }
            public DateTime EndAt { get; set; }
        }
        public class RecentSubmissionRow
        {
            public string SessionId { get; set; } = "";
            public string UserName { get; set; } = "";
            public string TestTitle { get; set; } = "";
            public double Score { get; set; }
            public bool IsPass { get; set; }
            public DateTime EndAt { get; set; }
        }
        public class SkillStat
        {
            public string Skill { get; set; } = "";
            public int Count { get; set; }
        }
    }
}
