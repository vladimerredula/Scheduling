using Microsoft.EntityFrameworkCore;
using Scheduling.Models;

namespace Scheduling
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Sector> Sectors { get; set; }
        public DbSet<Shift> Shifts { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Leave> Leaves { get; set; }
        public DbSet<Leave_type> Leave_types { get; set; }
        public DbSet<Schedule> Schedules { get; set; }
        public DbSet<Holiday> Holidays { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
    }
}
