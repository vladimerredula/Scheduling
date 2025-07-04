using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scheduling.Models;
using Scheduling.Services;

namespace Scheduling.Controllers
{
    [Authorize]
    public class HolidayController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly LogService<HolidayController> _log;

        public HolidayController(ApplicationDbContext context, LogService<HolidayController> logger)
        {
            _db = context;
            _log = logger;
        }

        public IActionResult Index()
        {
            ViewBag.Holidays = _db.Holidays.ToList();
            _log.LogInfo("Visited holiday index");

            return View();
        }

        // POST: Holiday/Add
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add([Bind("Date,Name,Type")] Holiday holiday)
        {
            if (ModelState.IsValid)
            {
                _db.Holidays.AddAsync(holiday);
                await _db.SaveChangesAsync();
                _log.LogInfo("Added holiday", holiday);
                TempData["toastMessage"] = "Successfully added Holiday!-success";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Holidays = _db.Holidays.ToList();

            return View(nameof(Index), holiday);
        }

        // POST: Holiday/Edit/id
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Holiday_ID,Date,Name,Type")] Holiday holiday)
        {
            if (id != holiday.Holiday_ID)
            {
                TempData["toastMessage"] = "Holiday IDs didn't match.-danger";
                _log.LogWarning($"Holiday IDs didn't match: {id} and {holiday.Holiday_ID}");
                return RedirectToAction(nameof(Index));
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _db.Holidays.Update(holiday);
                    await _db.SaveChangesAsync();
                    _log.LogInfo("Updated holiday", holiday);
                    TempData["toastMessage"] = "Successfully updated Holiday!-success";
                }
                catch (Exception ex)
                {
                    _log.LogError($"Unable to update Holiday with ID: {id}", ex);
                    TempData["toastMessage"] = "Unable to update Holiday.-danger";
                }

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Holidays = _db.Holidays.ToList();

            return View(nameof(Index), holiday);
        }


        // POST: Holiday/Delete/id
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var holiday = await _db.Holidays.FindAsync(id);
            if (holiday == null)
            {
                TempData["toastMessage"] = "Holiday not found-danger";
                _log.LogWarning($"Holiday ID: {id} was not found");
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _db.Holidays.Remove(holiday);
                await _db.SaveChangesAsync();
                _log.LogInfo("Deleted holiday", holiday);
                TempData["toastMessage"] = "Successfully deleted Holiday!-success";
            }
            catch (Exception ex)
            {
                _log.LogError($"Unable to delete Holiday with ID: {id}", ex);
                TempData["toastMessage"] = "Unable to delete Holiday.-danger";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> GetHoliday(int? id)
        {
            if (id == null || _db.Holidays == null)
            {
                _log.LogWarning($"Holiday table is empty");
                return NoContent();
            }

            var holiday = await _db.Holidays.FindAsync(id);
            if (holiday == null)
            {
                _log.LogWarning($"Holiday ID: {id} was not found");
                return NotFound(new { message = "Holiday not found!" });
            }

            return Ok(holiday);
        }
    }
}
