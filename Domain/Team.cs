namespace Employee_Survey.Domain
{
    public class Team
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
    }
}
