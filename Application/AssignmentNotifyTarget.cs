using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Employee_Survey.Domain;

namespace Employee_Survey.Application
{
    /// <summary>
    /// Gói thông tin để notify từng user (có thể gắn với một session cụ thể).
    /// </summary>
    public class AssignmentNotifyTarget
    {
        public required User User { get; set; }
        public required string SessionId { get; set; } // nếu chưa có session: để chuỗi rỗng
    }

    public interface INotificationService
    {
        /// <summary>
        /// Gửi thông báo/email tới danh sách user khi họ được assign vào test.
        /// Nếu SessionId rỗng sẽ dẫn người dùng vào trang /mytests.
        /// </summary>
        Task NotifyAssignmentsAsync(
            Test test,
            IEnumerable<AssignmentNotifyTarget> targets,
            DateTime startAtUtc,
            DateTime endAtUtc);
    }
}
