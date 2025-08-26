using System.ComponentModel.DataAnnotations;
using Employee_Survey.Domain;

namespace Employee_Survey.Models.Users
{
    public class UserFormViewModel
    {
        public string? Id { get; set; }

        [Required, Display(Name = "Full name")]
        public string Name { get; set; } = "";

        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Display(Name = "Role")]
        public Role Role { get; set; } = Role.Employee;

        [Required]
        public string Level { get; set; } = "Junior";

        [Display(Name = "Team")]
        public string TeamId { get; set; } = "";

        [Display(Name = "Department")]
        [StringLength(100)]
        public string? Department { get; set; } = "";  // ✅ Thêm mới

        // dùng khi tạo mới hoặc đổi mật khẩu
        [DataType(DataType.Password)]
        [Display(Name = "Password (leave blank to keep)")]
        public string? Password { get; set; }
    }
}
