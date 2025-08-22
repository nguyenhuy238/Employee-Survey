using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;

namespace Employee_Survey.Application
{
    public class AssignmentService
    {
        private readonly IRepository<Assignment> _asRepo;
        private readonly IRepository<User> _uRepo;
        private readonly IRepository<Test> _testRepo;

        public AssignmentService(IRepository<Assignment> a, IRepository<User> u, IRepository<Test> t) 
        { 
            _asRepo = a; 
            _uRepo = u; 
            _testRepo = t;
        }

        // Lấy các TestId hợp lệ cho user
        public async Task<List<string>> GetAvailableTestIdsAsync(string userId, DateTime nowUtc)
        {
            var u = await _uRepo.FirstOrDefaultAsync(x => x.Id == userId) ?? throw new Exception("User not found");
            var list = await _asRepo.GetAllAsync();
            var availableAssignmentIds = list.Where(a => a.StartAt <= nowUtc && nowUtc <= a.EndAt &&
                 ((a.TargetType == "User" && a.TargetValue == u.Id) ||
                  (a.TargetType == "Role" && a.TargetValue == u.Role.ToString()) ||
                  (a.TargetType == "Team" && a.TargetValue == u.TeamId)))
                .Select(a => a.TestId).Distinct().ToList(); // <-- Fixed here

            // Chỉ trả về test IDs của các test đã được publish
            var allTests = await _testRepo.GetAllAsync();
            var publishedTestIds = allTests.Where(t => t.IsPublished).Select(t => t.Id).ToHashSet();
            
            return availableAssignmentIds.Where(id => publishedTestIds.Contains(id)).ToList();
        }

        // Lấy danh sách test đầy đủ cho user
        public async Task<List<Test>> GetAvailableTestsAsync(string userId, DateTime nowUtc)
        {
            var testIds = await GetAvailableTestIdsAsync(userId, nowUtc);
            var allTests = await _testRepo.GetAllAsync();
            return allTests.Where(t => testIds.Contains(t.Id)).ToList();
        }
    }
}
