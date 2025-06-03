using Microsoft.EntityFrameworkCore;
using Scheduling.Models;
using Scheduling.Models.Templates;

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
        public DbSet<Schedule_order> Schedule_orders { get; set; }
        public DbSet<Schedule_shiftleader> Schedule_shiftleaders { get; set; }
        public DbSet<Holiday> Holidays { get; set; }

        // Templates
        public DbSet<Template> Templates { get; set; }
        public DbSet<Module> Modules { get; set; }
        public DbSet<Page> Pages { get; set; }
        public DbSet<Component> Components { get; set; }
        public DbSet<Template_module> Template_modules { get; set; }
        public DbSet<Template_page> Template_pages { get; set; }
        public DbSet<Template_component> Template_components { get; set; }

        public DbSet<Session> Sessions { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
    }
}
