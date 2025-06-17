using Microsoft.EntityFrameworkCore;
using Scheduling.Controllers;
using Scheduling.Models;

namespace Scheduling.Services
{
    public class ScheduleTokenService
    {
        private readonly ApplicationDbContext _db;
        private readonly LogService<ScheduleController> _log;

        public ScheduleTokenService(ApplicationDbContext db, LogService<ScheduleController> logger)
        {
            _db = db;
            _log = logger;
        }

        public async Task UpdateTokenAsync(int departmentId, int year, int month)
        {
            var existingToken = await _db.Edit_tokens
                .FirstOrDefaultAsync(t => 
                    t.Department_ID == departmentId && 
                    t.Year == year && 
                    t.Month == month);

            if (existingToken == null)
            {
                var newToken = new Edit_token
                {
                    Department_ID = departmentId,
                    Year = year,
                    Month = month,
                    Expiry = DateTime.Now.AddMinutes(30)
                };

                _db.Edit_tokens.Add(newToken);
            }
            else
            {
                existingToken.Expiry = DateTime.Now.AddMinutes(30);
                _db.Edit_tokens.Update(existingToken);
            }

            await _db.SaveChangesAsync();

            var department = (await _db.Departments.FindAsync(departmentId))?.Department_name ?? string.Empty;
            var date = new DateTime(year, month, 1);

            await _log.LogInfoAsync($"Updated Schedule Token for {department} {date.ToString("MMMM yyyy")}.");
        }
    }
}
