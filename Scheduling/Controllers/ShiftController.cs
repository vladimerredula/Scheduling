using Microsoft.AspNetCore.Mvc;
using Scheduling.Models;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Scheduling.Controllers
{
    public class ShiftController : Controller
    {
        private readonly ApplicationDbContext _db;

        public ShiftController(ApplicationDbContext context)
        {
            _db = context;
        }

        public async Task<IActionResult> Index()
        {
            if (!User.IsInRole("admin"))
            {
                var user = await ThisUser();
                ViewBag.Shifts = _db.Shifts.Where(s => s.Department_ID == user.Department_ID).ToList();
            } else
            {
                ViewBag.Shifts = _db.Shifts.ToList();
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
                return RedirectToAction(nameof(Index));
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _db.Shifts.Update(shift);
                    await _db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unable to update Shift: {id} - {ex}");
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
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _db.Shifts.Remove(shift);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to delete Shift: {id} - {ex}");
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> GetShift(int? id)
        {
            if (id == null || _db.Shifts == null)
            {
                return NoContent();
            }

            var shift = await _db.Shifts.FindAsync(id);
            if (shift == null)
            {
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
