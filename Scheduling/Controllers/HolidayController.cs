using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scheduling.Models;

namespace Scheduling.Controllers
{
    [Authorize]
    public class HolidayController : Controller
    {
        private readonly ApplicationDbContext _db;

        public HolidayController(ApplicationDbContext context)
        {
            _db = context;
        }

        public IActionResult Index()
        {
            ViewBag.Holidays = _db.Holidays.ToList();
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
                return RedirectToAction(nameof(Index));
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _db.Holidays.Update(holiday);
                    await _db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unable to update Holiday: {id} - {ex}");
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
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _db.Holidays.Remove(holiday);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to delete Holiday: {id} - {ex}");
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> GetHoliday(int? id)
        {
            if (id == null || _db.Holidays == null)
            {
                return NoContent();
            }

            var holiday = await _db.Holidays.FindAsync(id);
            if (holiday == null)
            {
                return NotFound(new { message = "Holiday not found!" });
            }

            return Ok(holiday);
        }
    }
}
