using Employee_Survey.Domain;
using BCrypt.Net;

namespace Employee_Survey.Infrastructure
{
    public static class Seeder
    {
        public static async Task RunAsync(IServiceProvider sp)
        {
            var users = sp.GetRequiredService<IRepository<User>>();
            var teams = sp.GetRequiredService<IRepository<Team>>();
            var questions = sp.GetRequiredService<IRepository<Question>>();
            var tests = sp.GetRequiredService<IRepository<Test>>();
            var assigns = sp.GetRequiredService<IRepository<Assignment>>();

            if (!(await users.GetAllAsync()).Any())
            {
                await users.InsertAsync(new User { Id = "u-admin", Name = "Admin", Email = "admin@local", Role = Role.Admin, PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123") });
                await users.InsertAsync(new User { Id = "u-hr", Name = "HR", Email = "hr@local", Role = Role.HR, PasswordHash = BCrypt.Net.BCrypt.HashPassword("hr123") });
                await users.InsertAsync(new User { Id = "u-emp", Name = "Alice", Email = "alice@local", Role = Role.Employee, Level = "Junior", TeamId = "t-a", PasswordHash = BCrypt.Net.BCrypt.HashPassword("alice123") });

                // THÊM TÀI KHOẢN MANAGER MẪU
                await users.InsertAsync(new User { Id = "u-manager", Name = "Manager", Email = "manager@local", Role = Role.Manager, Level = "Senior", TeamId = "t-a", PasswordHash = BCrypt.Net.BCrypt.HashPassword("manager123") });
            }

            if (!(await teams.GetAllAsync()).Any())
            {
                await teams.InsertAsync(new Team { Id = "t-a", Name = "Team A" });
            }

            if (!(await questions.GetAllAsync()).Any())
            {
                await questions.InsertAsync(new Question { Content = "C# là ngôn ngữ gì?", Type = QType.MCQ, Options = new() { "Ngôn ngữ lập trình", "Hệ điều hành", "CSDL", "Trình duyệt" }, Skill = "C#", Difficulty = "Junior" });
                await questions.InsertAsync(new Question { Content = ".NET là framework? (Đ/S)", Type = QType.MCQ, Skill = ".NET", Difficulty = "Junior" });
                await questions.InsertAsync(new Question { Content = "ASP.NET MVC là gì?", Type = QType.MCQ, Options = new() { "DB", "Web framework", "IDE", "OS" }, Skill = "ASP.NET", Difficulty = "Junior" });
                await questions.InsertAsync(new Question { Content = "HTTP là giao thức web? (Đ/S)", Type = QType.MCQ, Skill = "Web", Difficulty = "Junior" });
                await questions.InsertAsync(new Question { Content = "Razor là...", Type = QType.MCQ, Options = new() { "Template engine", "DB", "OS", "Shell" }, Skill = "ASP.NET", Difficulty = "Junior" });
            }

            if (!(await tests.GetAllAsync()).Any())
            {
                var test = new Test
                {
                    Id = "t-basic",
                    Title = "Basic .NET",
                    DurationMinutes = 10,
                    PassScore = 3,
                    SkillFilter = "ASP.NET",
                    RandomMCQ = 2,
                    RandomTF = 1,
                    RandomEssay = 0,
                    IsPublished = true,
                    CreatedAt = DateTime.UtcNow,
                    PublishedAt = DateTime.UtcNow
                };
                await tests.InsertAsync(test);
            }

            if (!(await assigns.GetAllAsync()).Any())
            {
                await assigns.InsertAsync(new Assignment
                {
                    TestId = "t-basic",
                    TargetType = "User",
                    TargetValue = "u-emp",
                    StartAt = DateTime.UtcNow.AddDays(-1),
                    EndAt = DateTime.UtcNow.AddDays(30)
                });
            }
        }
    }
}
