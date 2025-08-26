using System.Collections.Generic;
using Employee_Survey.Domain;

namespace Employee_Survey.Models
{
    public class AssignUsersViewModel
    {
        public string TestId { get; set; } = string.Empty;
        public string TestTitle { get; set; } = string.Empty;

        // Danh sách user hiển thị (đã lọc theo Department nếu có)
        public List<User> Users { get; set; } = new();

        // Những user đã được assign sẵn
        public HashSet<string> AssignedUserIds { get; set; } = new();

        // Bộ lọc Department
        public List<string> Departments { get; set; } = new();
        public string? SelectedDepartment { get; set; }
    }
}
