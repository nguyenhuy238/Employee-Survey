using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;

namespace Employee_Survey.Application
{
    public class AssignmentService
    {
        private readonly IRepository<Assignment> _asRepo;
        private readonly IRepository<User> _uRepo;

        public AssignmentService(IRepository<Assignment> a, IRepository<User> u) { _asRepo = a; _uRepo = u; }

        // Lấy các TestId hợp lệ cho user
        public async Task<List<string>> GetAvailableTestIdsAsync(string userId, DateTime nowUtc)
        {
            var u = await _uRepo.FirstOrDefaultAsync(x => x.Id == userId) ?? throw new Exception("User not found");
            var list = await _asRepo.GetAllAsync();
            return list.Where(a => a.StartAt <= nowUtc && nowUtc <= a.EndAt &&
                 ((a.TargetType == "User" && a.TargetValue == u.Id) ||
                  (a.TargetType == "Role" && a.TargetValue == u.Role.ToString()) ||
                  (a.TargetType == "Team" && a.TargetValue == u.TeamId)))
                .Select(a => a.TestId).Distinct().ToList();
        }
    }
}
