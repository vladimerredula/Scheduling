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
            var userOrder = await _db.Employee_orders
                .Include(o => o.User)
                .Where(o =>
                    o.User.Department_ID == departmentId &&
                    (o.Year < year || (o.Year == year && o.Month <= month))) // Filter for orders earlier or equal to the selected month and year
                .OrderBy(o => o.Year)  // Order by Year first to get the latest orders
                .ThenBy(o => o.Month)  // Then by Month within the same year
                .ThenBy(o => o.Order_index) // Optional: If you want to order by Order_index
                .Select(o => o.Personnel_ID)
                .ToListAsync();

            var baseQuery = await _db.Users
                .Include(u => u.Sector)
                .Where(u =>
                    u.Privilege_ID != 0 &&
                    u.Privilege_ID != 4 &&
                    u.Department_ID == departmentId &&
                    u.Status == 1)
                .ToListAsync(); // Execute once, filter and sort in memory

            var usersInOrder = baseQuery
                .Where(u => userOrder.Contains(u.Personnel_ID))
                .OrderBy(u => userOrder.IndexOf(u.Personnel_ID))
                .ToList();

            var usersNotInOrder = baseQuery
                .Where(u => !userOrder.Contains(u.Personnel_ID))
                .OrderBy(u => u?.Sector?.Order)
                .ThenByDescending(u => u.Privilege_ID)
                .ThenBy(u => u.First_name)
                .ThenBy(u => u.Last_name)
                .ToList();

            var users = usersInOrder.Concat(usersNotInOrder).ToList();

            var schedules = await _db.Schedules
                            .Include(sc => sc.Shift)
                            .Where( sc => sc.Date.Month == month && sc.Date.Year == year)
                            .Select(sc => new
                            {
                                Personnel_ID = sc.Personnel_ID,
                                Shift = sc.Shift.Shift_name,
                                Comment = sc.Comment,
                                Date = sc.Date
                            })
                            .ToListAsync<dynamic>();

            var shifts = await _db.Shifts.Where(s => s.Department_ID == departmentId).ToListAsync();

            var leaves = await _db.Leaves
                            .Include(l => l.Leave_type)
                            .Where(l => 
                            (l.Date_start.Year == year && l.Date_start.Month == month) || (l.Date_end.Year == year && l.Date_end.Month == month) &&
                            l.User.Department_ID == departmentId)
                            .ToListAsync();

            var holidays = await _db.Holidays.Where(h => h.Date.Year == year && h.Date.Month == month).ToListAsync();

            var department = await _db.Departments.FindAsync(departmentId);

            var excelFile = _excel.Schedule(users.ToList<dynamic>(), schedules, shifts, leaves, holidays, department.Department_name, month, year);

            var date = new DateTime(year, month, 1);

            await _log.LogInfoAsync($"Downloaded {date.ToString("yyyy.MM")} {department.Department_name} Schedule");

            return File(excelFile, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{date.ToString("yyyy.MM")} {department.Department_name}.xlsx");
        }
    }
}
