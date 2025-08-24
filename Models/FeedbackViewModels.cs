// Employee_Survey.Models/FeedbackViewModels.cs
using System.ComponentModel.DataAnnotations;

namespace Employee_Survey.Models
{
    public class FeedbackCreateViewModel
    {
        [Required]
        public string SessionId { get; set; } = "";

        [Display(Name = "Nội dung góp ý")]
        [Required, StringLength(2000)]
        public string Content { get; set; } = "";

        [Display(Name = "Đánh giá")]
        [Range(1, 5)]
        public int Rating { get; set; } = 5;

        // Hiển thị tham chiếu
        public string TestTitle { get; set; } = "";
    }

    public class HrFeedbackItemVm
    {
        public string FeedbackId { get; set; } = "";
        public string SessionId { get; set; } = "";
        public string UserName { get; set; } = "";
        public string UserEmail { get; set; } = "";
        public string TestTitle { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public int Rating { get; set; }
        public string Content { get; set; } = "";
    }
}
