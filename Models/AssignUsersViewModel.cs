using System.Collections.Generic;
using Employee_Survey.Domain;

namespace Employee_Survey.Models
{
	public class AssignUsersViewModel
	{
		public string TestId { get; set; } = string.Empty;
		public string TestTitle { get; set; } = string.Empty;
		public List<User> Users { get; set; } = new();
		public HashSet<string> AssignedUserIds { get; set; } = new();
	}
}


