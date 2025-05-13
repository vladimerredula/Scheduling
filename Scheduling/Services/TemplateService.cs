using Microsoft.EntityFrameworkCore;
using Scheduling.Models.Templates;

namespace Scheduling.Services
{
    public class TemplateService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TemplateService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _db = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public List<Module>? GetUserTemplate()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var userIdValue = httpContext?.User?.FindFirst("Personnelid")?.Value;

            if (!int.TryParse(userIdValue, out var userId))
                return null;

            var modules = _db.Users
                .Where(u => u.Personnel_ID == userId)
                .Include(u => u.Template)
                    .ThenInclude(t => t.Modules)
                        .ThenInclude(m => m.Pages)
                            .ThenInclude(p => p.Components)
                .AsSplitQuery()
                .SelectMany(u => u.Template.Modules.Select(m => new Module
                {
                    Module_name = m.Module.Module_name,
                    Controller_name = m.Module.Controller_name,
                    Pages = m.Pages.Select(p => new Page
                    {
                        Page_name = p.Page.Page_name,
                        Action_name = p.Page.Action_name,
                        Components = p.Components.Select(c => new Component
                        {
                            Component_name = c.Component.Component_name,
                            Component_abbreviation = c.Component.Component_abbreviation
                        }).ToList()
                    }).ToList()
                }))
                .ToList();

            return modules;
        }

        public List<Component>? GetComponentPermission()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var routeData = httpContext.GetRouteData();
            var controller = routeData?.Values["controller"]?.ToString();
            var action = routeData?.Values["action"]?.ToString();

            switch (action)
            {
                case "GetScheduleByMonth":
                    action = "Index";
                    break;
                case "GetCalendar":
                    action = "Calendar";
                    break;
                default:
                    break;
            }

            var modules = GetUserTemplate();

            if (modules == null)
                return new List<Component>();

            var components = modules
                .Where(m => m.Controller_name == controller)
                .SelectMany(m => m.Pages)
                .Where(p => p.Action_name == action)
                .SelectMany(p => p.Components)
                .ToList();

            return components;
        }

        public bool HasPermission(string keyWord)
        {
            var components = GetComponentPermission();

            return components.Any(c => c.Component_abbreviation == keyWord);
        }
    }
}
