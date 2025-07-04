using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Scheduling.Models;
using Scheduling.Services;
using System.Security.Claims;

namespace Scheduling.Controllers
{
    public class ShiftController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly LogService<ShiftController> _log;

        public ShiftController(ApplicationDbContext context, LogService<ShiftController> logger)
        {
            _db = context;
            _log = logger;
        }

        public async Task<IActionResult> Index(int departmentId = 1)
        {
            if (User.IsInRole("manager"))
            {
                var user = await ThisUser();

                ViewBag.Shifts = _db.Shifts
                    .Where(l => 
                        l.Department_ID == user.Department_ID && 
                        l.Status == 1)
                    .ToList();

                ViewBag.Departments = new SelectList(_db.Departments.ToList(), "Department_ID", "Department_name", user.Department_ID);
            }
            else if (User.IsInRole("topManager") || User.IsInRole("admin"))
            {
                ViewBag.Shifts = _db.Shifts
                    .Where(l => 
                        l.Department_ID == departmentId &&
                        l.Status == 1)
                    .ToList();

                ViewBag.Departments = new SelectList(_db.Departments.ToList(), "Department_ID", "Department_name", departmentId);
            }
            _log.LogInfo($"Visited shifts");

            return View();
        }

        // POST: Shift/Add
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add([Bind("Shift_name,Acronym,Time_start,Time_end,Department_ID,Pattern,Status")] Shift shift)
        {
            if (ModelState.IsValid)
            {
                var user = await ThisUser();

                await _db.Shifts.AddAsync(shift);
                await _db.SaveChangesAsync();
                _log.LogInfo($"Added new Shift", shift);

                TempData["toastMessage"] = "Successfully added shift!-success";
                return RedirectToAction(nameof(Index), new { departmentId = shift.Department_ID});
            }

            ViewBag.Shifts = _db.Shifts.ToList();

            return View(nameof(Index), shift);
        }

        // POST: Shift/Edit/id
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Shift_ID,Shift_name,Acronym,Time_start,Time_end,Pattern,Department_ID,Status")] Shift shift)
        {
            if (id != shift.Shift_ID)
            {
                TempData["toastMessage"] = "Shift IDs didn't match.-danger";
                _log.LogWarning($"Shift IDs didn't match: {id} and {shift.Shift_ID}");
                return RedirectToAction(nameof(Index), new { departmentId = shift.Department_ID });
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _db.Shifts.Update(shift);
                    await _db.SaveChangesAsync();
                    _log.LogInfo($"Updated shift", shift);
                    TempData["toastMessage"] = "Successfully updated shift!-success";
                }
                catch (Exception ex)
                {
                    _log.LogError($"Unable to update shift with ID: {id}", ex);
                    TempData["toastMessage"] = "Unable to update shift.-danger";
                }

                return RedirectToAction(nameof(Index), new { departmentId = shift.Department_ID });
            }

            ViewBag.Shifts = _db.Shifts.ToList();

            return View(nameof(Index), shift);
        }


        // POST: Shift/Delete/id
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var shift = await _db.Shifts.FindAsync(id);
            if (shift == null)
            {
                TempData["toastMessage"] = "Shift not found.-danger";
                _log.LogWarning($"Shift ID: {id} was not found");
                return RedirectToAction(nameof(Index), new { departmentId = shift.Department_ID });
            }

            try
            {
                _db.Shifts.Update(shift);
                shift.Status = 0;
                await _db.SaveChangesAsync();
                _log.LogInfo($"Removed shift", shift);
                TempData["toastMessage"] = "Successfully removed shift!-success";
            }
            catch (Exception ex)
            {
                _log.LogError($"Unable to remove shift with ID: {id}", ex);
                TempData["toastMessage"] = "Unable to remove shift.-danger";
            }

            return RedirectToAction(nameof(Index), new { departmentId = shift.Department_ID });
        }

        [HttpPost]
        public async Task<IActionResult> GetShift(int? id)
        {
            if (id == null || _db.Shifts == null)
            {
                _log.LogWarning($"Shift table is empty");
                return NoContent();
            }

            var shift = await _db.Shifts.FindAsync(id);
            if (shift == null)
            {
                _log.LogWarning($"Shift ID: {id} was not found");
                return NotFound(new { message = "Shift not found!" });
            }

            return Ok(shift);
        }

        public int GetPersonnelID()
        {
            var personnelId = int.Parse(User.FindFirstValue("Personnelid"));

            return personnelId;
        }

        public async Task<string> GetUsername()
        {
            var user = await ThisUser();
            return user.Username;
        }

        public async Task<User> ThisUser()
        {
            return _db.Users.Find(GetPersonnelID());
        }
    }
}
