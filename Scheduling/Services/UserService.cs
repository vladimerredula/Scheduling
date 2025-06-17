using Microsoft.EntityFrameworkCore;
using Scheduling.Models;

namespace Scheduling.Services
{
    public class UserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ApplicationDbContext _db;

        public UserService(IHttpContextAccessor httpContextAccessor, ApplicationDbContext db)
        {
            _httpContextAccessor = httpContextAccessor;
            _db = db;
        }

        public int GetPersonnelId()
        {
            var personnelIdStr = _httpContextAccessor.HttpContext?.User?.FindFirst("Personnelid")?.Value;
            return int.TryParse(personnelIdStr, out int personnelId) ? personnelId : 0;
        }

        public async Task<User?> GetUser(int? id = null)
        {
            var userId = id ?? GetPersonnelId();
            return await _db.Users.FindAsync(userId);
        }

        public async Task<string> GetUsername(int? id = null)
        {
            return (await GetUser(id))?.Username ?? string.Empty;
        }

        public async Task<string> GetFullname(int? id = null)
        {
            return (await GetUser(id))?.Full_name ?? string.Empty;
        }

        public async Task<int> GetDepartmentId(int? id = null)
        {
            return (await GetUser(id))?.Department_ID ?? 1;
        }

        private IQueryable<User> BaseUserQuery =>
            _db.Users
                .Include(u => u.Sector)
                .Where(u => u.Privilege_ID != 0 && u.Privilege_ID != 4);

        public async Task<List<User>> GetUsers(
            int? status = null,
            int? departmentId = null,
            int? sectorId = null,
            bool activeOnly = false,
            int? month = null,
            int? year = null)
        {
            var query = BaseUserQuery;

            if (status.HasValue)
                query = query.Where(u => u.Status == status.Value);

            if (departmentId.HasValue && departmentId > 0)
                query = query.Where(u => u.Department_ID == departmentId);

            if (sectorId.HasValue && sectorId > 0)
                query = query.Where(u => u.Sector_ID == sectorId);

            if (activeOnly)
            {
                int m = month ?? DateTime.Now.Month;
                int y = year ?? DateTime.Now.Year;

                var firstDay = new DateTime(y, m, 1);
                var lastDay = new DateTime(y, m, DateTime.DaysInMonth(y, m));

                query = query.Where(u =>
                    (u.Date_hired == null || u.Date_hired <= lastDay) &&
                    (u.Last_day == null || u.Last_day >= firstDay));
            }

            return await query.ToListAsync();
        }

        // Get all users in the department
        public async Task<List<User>> GetActiveUsers(int month, int year, int? departmentId = null)
        {
            if (departmentId == null || departmentId == 0)
                departmentId = await GetDepartmentId();

            return await GetUsers(
                month: month, 
                year: year,
                departmentId: departmentId) ?? new List<User>();
        }
    }
}
