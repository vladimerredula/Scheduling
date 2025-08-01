﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Scheduling.Models.Templates;

namespace Scheduling.Services
{
    [Authorize]
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
                .Where(u => u.Personnel_ID == userId && u.Template.App_name == "SCH")
                .Include(u => u.Template)
                    .ThenInclude(t => t.Modules)
                        .ThenInclude(m => m.Pages)
                            .ThenInclude(p => p.Components)
                .AsSplitQuery()
                .SelectMany(u => u.Template.Modules.Select(m => new Module
                {
                    Module_ID = m.Module.Module_ID,
                    Module_name = m.Module.Module_name,
                    Controller_name = m.Module.Controller_name,
                    Pages = m.Pages.Select(p => new Page
                    {
                        Page_ID = p.Page.Page_ID,
                        Page_name = p.Page.Page_name,
                        Controller_name = p.Page.Controller_name,
                        Action_name = p.Page.Action_name,
                        Components = p.Components.Select(c => new Component
                        {
                            Page_ID = p.Page.Page_ID,
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
                case "LoadScheduleView":
                    action = "ScheduleView";
                    break;
                case "Apply":
                    action = "DepartmentLeaves";
                    break;
                case "Add":
                case "AddLeave":
                    if (controller == "Leave")
                    {
                        action = "DepartmentLeaves";
                    } else
                    {
                        action = "Index";
                    }
                    break;
                default:
                    break;
            }

            var modules = GetUserTemplate();

            if (modules == null)
                return new List<Component>();

            var components = modules
                .SelectMany(m => m.Pages)
                .Where(p => p.Controller_name == controller && p.Action_name == action)
                .SelectMany(p => p.Components)
                .ToList();

            return components;
        }

        public List<Page>? GetPagePermission()
        {
            var modules = GetUserTemplate();
            if (modules == null)
                return new List<Page>();

            var pages = modules
                .SelectMany(m => m.Pages)
                .ToList();

            return pages;
        }

        public bool HasPermission(string keyWord)
        {
            var pages = GetPagePermission();
            var components = GetComponentPermission();

            return components.Any(c => c.Component_abbreviation == keyWord) || pages.Any(p => p.Page_name == keyWord);
        }

        public bool HasPermission(string pageName, string keyWord)
        {
            var page = _db.Pages.FirstOrDefault(p => p.Page_name == pageName);
            var modules = GetUserTemplate(); 
            var components = modules.Where(m => m.Module_ID == page.Module_ID)
            .SelectMany(m => m.Pages)
            .SelectMany(p => p.Components)
            .ToList();

            return components.Any(c => c.Page_ID == page.Page_ID && c.Component_abbreviation == keyWord);
        }
    }
}
