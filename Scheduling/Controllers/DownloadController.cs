using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scheduling.Services;

namespace Scheduling.Controllers
{
    public class DownloadController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ExcelService _excel;
        private readonly LogService<DownloadController> _log;

        public DownloadController(ApplicationDbContext dbContext, LogService<DownloadController> log)
        {
            _db = dbContext;
            _log = log;
            _excel = new ExcelService();
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> DownloadSchedule(int month, int year, int departmentId)
        {
            var firstDayOfMonth = new DateTime(year, month, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            // Get relevant employee orders for the selected year and month (latest per user)
            var employeeOrders = await _db.Schedule_orders
                .Include(o => o.User)
                .Where(o =>
                    o.User.Department_ID == departmentId &&
                    (o.Year < year || (o.Year == year && o.Month <= month)))
                .GroupBy(o => o.Personnel_ID)
                .Select(g => g
                    .OrderByDescending(o => o.Year)
                    .ThenByDescending(o => o.Month)
                    .First())
                .ToListAsync();

            // Map Personnel_ID to Order_index and Sector_ID
            var orderLookup = employeeOrders
                .OrderBy(o => o.Order_index)
                .Select((o, index) => new
                {
                    PersonnelId = o.Personnel_ID,
                    SectorId = o.Sector_ID,
                    Index = index
                })
                .ToList();

            // Get all users in the department
            var baseUsers = await _db.Users
                .Include(u => u.Sector)
                .Where(u =>
                    u.Privilege_ID != 0 &&
                    u.Privilege_ID != 4 &&
                    u.Department_ID == departmentId &&
                    u.Status == 1 &&
                    (u.Date_hired == null || u.Date_hired.Value.Date <= lastDayOfMonth.Date) &&
                    (u.Last_day == null || u.Last_day.Value.Date >= firstDayOfMonth.Date))
                .ToListAsync();

            // Override Sector_IDs based on latest Employee_order
            foreach (var baseUser in baseUsers)
            {
                var match = orderLookup.FirstOrDefault(o => o.PersonnelId == baseUser.Personnel_ID);
                if (match != null && match.SectorId.HasValue)
                {
                    baseUser.Sector_ID = match.SectorId.Value;
                    baseUser.Sector = await _db.Sectors.FindAsync(match.SectorId.Value);
                }
            }

            // Split and sort
            var usersInOrder = baseUsers
                .Where(u => orderLookup.Any(o => o.PersonnelId == u.Personnel_ID))
                .OrderBy(u => orderLookup.First(o => o.PersonnelId == u.Personnel_ID).Index);

            var usersNotInOrder = baseUsers
                .Where(u => orderLookup.All(o => o.PersonnelId != u.Personnel_ID))
                .OrderBy(u => u?.Sector?.Order)
                .ThenByDescending(u => u.Privilege_ID)
                .ThenBy(u => u.First_name)
                .ThenBy(u => u.Last_name);

            // Final user list
            var users = usersInOrder.Concat(usersNotInOrder).ToList();

            var schedules = await _db.Schedules
                            .Include(sc => sc.Shift)
                            .Where( sc => sc.Date.Month == month && sc.Date.Year == year)
                            .Select(sc => new
                            {
                                Personnel_ID = sc.Personnel_ID,
                                Shift = sc.Shift.Acronym ?? sc.Shift.Shift_name,
                                Time_in = sc.Time_in,
                                Time_out = sc.Time_out,
                                Comment = sc.Comment,
                                Date = sc.Date
                            })
                            .ToListAsync<dynamic>();

            var shifts = await _db.Shifts.
                Where(s => 
                    s.Department_ID == departmentId && 
                    s.Status == 1)
                .ToListAsync();

            var leaves = await _db.Leaves
                            .Include(l => l.Leave_type)
                            .Where(l =>
                                l.Status == "Approved" && 
                                l.User.Department_ID == departmentId &&
                                l.Date_start.Date <= lastDayOfMonth.Date &&
                                l.Date_end.Date >= firstDayOfMonth.Date)
                            .ToListAsync();

            var holidays = await _db.Holidays.Where(h => h.Date.Year == year && h.Date.Month == month).ToListAsync();

            var department = await _db.Departments.FindAsync(departmentId);

            var excelFile = _excel.Schedule(users.ToList<dynamic>(), schedules, shifts, leaves, holidays, department.Department_name, month, year);

            await _log.LogInfoAsync($"Downloaded {firstDayOfMonth.ToString("yyyy.MM")} {department.Department_name} Schedule");

            return File(excelFile, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{firstDayOfMonth.ToString("yyyy.MM")} {department.Department_name}.xlsx");
        }
    }
}
