namespace Employee_Survey.Domain
{
    public class User
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public Role Role { get; set; } = Role.Employee;
        public string Level { get; set; } = "Junior";
        public string TeamId { get; set; } = "";
        public string Department { get; set; } = ""; 
        public string PasswordHash { get; set; } = "";
    }
}
