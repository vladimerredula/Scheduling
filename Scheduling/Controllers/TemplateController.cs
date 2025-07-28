using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scheduling.Models.Templates;
using Scheduling.Services;

namespace Scheduling.Controllers
{
    [Authorize(Roles = "admin")]
    public class TemplateController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly LogService<TemplateController> _log;

        public TemplateController(ApplicationDbContext dbContext, LogService<TemplateController> logger)
        {
            _db = dbContext;
            _log = logger;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Modules = await _db.Modules
                .Include(m => m.Pages)
                .ThenInclude(p => p.Components)
                .Where(t => t.App_name == "SCH")
                .ToListAsync();

            _log.LogInfo("Visited schedules");
            return View();
        }

        [HttpGet]
        public IActionResult GetTemplates()
        {
            var templates = _db.Templates
                .Include(t => t.Modules)
                    .ThenInclude(t => t.Pages)
                        .ThenInclude(t => t.Components)
                .Where(t => t.App_name == "SCH")
                .AsSplitQuery()
                .Select(t => new {
                    t.Template_ID,
                    t.Template_name,
                    Modules = t.Modules.Select(m => new {
                        m.Module_ID,
                        m.Module.Module_name,
                        Pages = m.Pages.Select(p => new {
                            p.Page_ID,
                            p.Page.Page_name,
                            Components = p.Components.Select(c => new
                            {
                                c.Component_ID,
                                c.Component.Component_name
                            })
                        })
                    })
                })
                .ToList();

            return Json(templates);
        }

        [HttpPost]
        public IActionResult GetTemplate(int id)
        {
            var template = _db.Templates
                .Include(t => t.Modules)
                    .ThenInclude(t => t.Pages)
                        .ThenInclude(t => t.Components)
                .Select(t => new {
                    t.Template_ID,
                    t.Template_name,
                    Modules = t.Modules.Select(m => new {
                        m.Module_ID,
                        m.Module.Module_name,
                        Pages = m.Pages.Select(p => new {
                            p.Page_ID,
                            p.Page.Page_name,
                            Components = p.Components.Select(c => new
                            {
                                c.Component_ID,
                                c.Component.Component_name
                            })
                        })
                    })
                })
                .FirstOrDefault(t => t.Template_ID == id);

            return Json(template);
        }

        public async Task<IActionResult> Add(string Template_name, List<int> selectedModules, List<string> selectedPages, List<string> selectedComponents)
        {
            // Create a new template
            var template = new Template
            {
                Template_name = Template_name,
                App_name = "SCH"
            };

            // Save the template
            await _db.Templates.AddAsync(template);
            await _db.SaveChangesAsync();

            // Save the selected modules
            foreach (var moduleId in selectedModules)
            {
                var templateModule = new Template_module
                {
                    Template_ID = template.Template_ID,
                    Module_ID = moduleId
                };

                await _db.Template_modules.AddAsync(templateModule);
            }

            await _db.SaveChangesAsync();

            // Save the selected pages
            foreach (var item in selectedPages)
            {
                var ids = item.Split("-");
                var templateModule = await _db.Template_modules
                    .FirstOrDefaultAsync(m => m.Template_ID == template.Template_ID && m.Module_ID == int.Parse(ids[0]));

                var templatePage = new Template_page
                {
                    Template_ID = template.Template_ID,
                    Page_ID = int.Parse(ids[1]),
                    Template_module_ID = templateModule.Template_module_ID
                };

                await _db.Template_pages.AddAsync(templatePage);
            }

            await _db.SaveChangesAsync();

            // Save the selected components
            foreach (var item in selectedComponents)
            {
                var ids = item.Split("-");
                var templatePage = await _db.Template_pages
                    .FirstOrDefaultAsync(m => m.Template_ID == template.Template_ID && m.Page_ID == int.Parse(ids[0]));

                var templateComponent = new Template_component
                {
                    Template_ID = template.Template_ID,
                    Component_ID = int.Parse(ids[1]),
                    Template_page_ID = templatePage.Template_page_ID,
                };

                await _db.Template_components.AddAsync(templateComponent);
            }

            await _db.SaveChangesAsync();

            TempData["toastMessage"] = "Successfully added template!-success";
            _log.LogInfo("Added template", template);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int Template_ID, string Template_name, List<int> selectedModules, List<string> selectedPages, List<string> selectedComponents)
        {
            var template = await _db.Templates.FindAsync(Template_ID);

            if (template == null)
            {
                TempData["toastMessage"] = "Template not found.-danger";
                return RedirectToAction(nameof(Index));
            }

            template.Template_name = Template_name;
            await _db.SaveChangesAsync();

            // Remove existing modules,pages, and components connected to the template
            await RemoveTemplateEntitiesAsync(_db.Template_modules.Where(t => t.Template_ID == template.Template_ID));
            await RemoveTemplateEntitiesAsync(_db.Template_pages.Where(p => p.Template_ID == template.Template_ID));
            await RemoveTemplateEntitiesAsync(_db.Template_components.Where(c => c.Template_ID == template.Template_ID));
            await _db.SaveChangesAsync();

            // Save the selected modules
            foreach (var moduleId in selectedModules)
            {
                var templateModule = new Template_module
                {
                    Template_ID = template.Template_ID,
                    Module_ID = moduleId
                };

                await _db.Template_modules.AddAsync(templateModule);
            }

            await _db.SaveChangesAsync();

            // Save the selected pages
            foreach (var item in selectedPages)
            {
                var ids = item.Split("-");
                var templateModule = await _db.Template_modules
                    .FirstOrDefaultAsync(m => m.Template_ID == template.Template_ID && m.Module_ID == int.Parse(ids[0]));

                var templatePage = new Template_page
                {
                    Template_ID = template.Template_ID,
                    Page_ID = int.Parse(ids[1]),
                    Template_module_ID = templateModule.Template_module_ID
                };

                await _db.Template_pages.AddAsync(templatePage);
            }

            await _db.SaveChangesAsync();

            // Save the selected components
            foreach (var item in selectedComponents)
            {
                var ids = item.Split("-");
                var templatePage = await _db.Template_pages
                    .FirstOrDefaultAsync(m => m.Template_ID == template.Template_ID && m.Page_ID == int.Parse(ids[0]));

                var templateComponent = new Template_component
                {
                    Template_ID = template.Template_ID,
                    Component_ID = int.Parse(ids[1]),
                    Template_page_ID = templatePage.Template_page_ID,
                };

                await _db.Template_components.AddAsync(templateComponent);
            }

            await _db.SaveChangesAsync();

            TempData["toastMessage"] = "Successfully updated template!-success";
            _log.LogInfo("Updated template", template);

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var template = await _db.Templates.FindAsync(id);

            if (template == null)
            {
                TempData["toastMessage"] = "Template not found.-danger";
                return RedirectToAction(nameof(Index));
            }

            await RemoveTemplateEntitiesAsync(_db.Templates.Where(t => t.Template_ID == template.Template_ID));
            await RemoveTemplateEntitiesAsync(_db.Template_modules.Where(t => t.Template_ID == template.Template_ID));
            await RemoveTemplateEntitiesAsync(_db.Template_pages.Where(p => p.Template_ID == template.Template_ID));
            await RemoveTemplateEntitiesAsync(_db.Template_components.Where(c => c.Template_ID == template.Template_ID));
            await _db.SaveChangesAsync();

            TempData["toastMessage"] = "Successfully deleted template!-success";
            _log.LogInfo("Deleted template", template);

            return RedirectToAction(nameof(Index));
        }

        async Task RemoveTemplateEntitiesAsync<T>(IQueryable<T> query) where T : class
        {
            var items = await query.ToListAsync();
            if (items.Any())
                _db.RemoveRange(items);
        }
    }
}
