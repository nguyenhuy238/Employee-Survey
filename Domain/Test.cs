namespace Employee_Survey.Domain
{
    public class Test
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "";
        public int DurationMinutes { get; set; } = 30;
        public int PassScore { get; set; } = 5;
        public string SkillFilter { get; set; } = "C#";
        public int RandomMCQ { get; set; } = 5;
        public int RandomTF { get; set; } = 5;
        public int RandomEssay { get; set; } = 0;
    }
}
