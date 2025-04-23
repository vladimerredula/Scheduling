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
                    .Where(l => l.Department_ID == user.Department_ID)
                    .ToList();
            }
            else if (User.IsInRole("topManager") || User.IsInRole("admin"))
            {
                ViewBag.Shifts = _db.Shifts
                    .Where(l => l.Department_ID == departmentId)
                    .ToList();

                ViewBag.Departments = new SelectList(_db.Departments.ToList(), "Department_ID", "Department_name", departmentId);
            }

            return View();
        }

        // POST: Shift/Add
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add([Bind("Shift_name,Time_start,Time_end,Pattern")] Shift shift)
        {
            if (ModelState.IsValid)
            {
                var user = await ThisUser();
                shift.Department_ID = user.Department_ID;

                await _db.Shifts.AddAsync(shift);
                await _db.SaveChangesAsync();

                await _log.LogInfoAsync($"Added new Shift", shift);

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Shifts = _db.Shifts.ToList();

            return View(nameof(Index), shift);
        }

        // POST: Shift/Edit/id
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Shift_ID,Shift_name,Time_start,Time_end,Pattern,Department_ID")] Shift shift)
        {
            if (id != shift.Shift_ID)
            {
                TempData["toastMessage"] = "Shift IDs didn't match.-danger";
                await _log.LogWarningAsync($"Shift IDs didn't match: {id} and {shift.Shift_ID}");
                return RedirectToAction(nameof(Index));
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _db.Shifts.Update(shift);
                    await _db.SaveChangesAsync();
                    await _log.LogInfoAsync($"Updated shift", shift);
                }
                catch (Exception ex)
                {
                    await _log.LogErrorAsync($"Unable to update shift with ID: {id}", ex);
                    TempData["toastMessage"] = "Unable to update shift.-danger";
                }

                return RedirectToAction(nameof(Index));
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
                await _log.LogWarningAsync($"Shift ID: {id} was not found");
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _db.Shifts.Remove(shift);
                await _db.SaveChangesAsync();
                await _log.LogInfoAsync($"Deleted shift", shift);
            }
            catch (Exception ex)
            {
                await _log.LogErrorAsync($"Unable to delete shift with ID: {id}", ex);
                TempData["toastMessage"] = "Unable to delete shift.-danger";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> GetShift(int? id)
        {
            if (id == null || _db.Shifts == null)
            {
                await _log.LogWarningAsync($"Shift table is empty");
                return NoContent();
            }

            var shift = await _db.Shifts.FindAsync(id);
            if (shift == null)
            {
                await _log.LogWarningAsync($"Shift ID: {id} was not found");
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
