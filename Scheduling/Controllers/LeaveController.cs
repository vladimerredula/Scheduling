using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Scheduling.Models;
using System.Security.Claims;

namespace Scheduling.Controllers
{
    [Authorize]
    public class LeaveController : Controller
    {
        private readonly ApplicationDbContext _db;

        public LeaveController(ApplicationDbContext context)
        {
            _db = context;
        }

        public async Task<IActionResult> Index(int departmentId = 1)
        {
            if (User.IsInRole("member") || User.IsInRole("shiftLeader"))
            {
                ViewBag.Leaves = _db.Leaves
                    .Include(l => l.User)
                    .Include(l => l.Leave_type)
                    .Include(l => l.Approver1)
                    .Include(l => l.Approver2)
                    .Where(l => l.Personnel_ID == GetPersonnelID())
                    .ToList();
            } else if (User.IsInRole("manager")) {
                var user = await ThisUser();

                ViewBag.Leaves = _db.Leaves
                    .Include(l => l.User)
                    .Include(l => l.Leave_type)
                    .Include(l => l.Approver1)
                    .Include(l => l.Approver2)
                    .Where(l => l.User.Department_ID == user.Department_ID)
                    .ToList();
            } else if (User.IsInRole("topManager") || User.IsInRole("admin"))
            {
                ViewBag.Leaves = _db.Leaves
                    .Include(l => l.User)
                    .Include(l => l.Leave_type)
                    .Include(l => l.Approver1)
                    .Include(l => l.Approver2)
                    .Where(l => l.User.Department_ID == departmentId)
                    .ToList();

                ViewBag.Departments = new SelectList(_db.Departments.ToList(), "Department_ID", "Department_name", departmentId);
            }

            ViewBag.LeaveTypes = new SelectList(_db.Leave_types.ToList(), "Leave_type_ID", "Leave_type_name");

            return View();
        }

        public async Task<IActionResult> DepartmentLeaves(int departmentId = 1)
        {
            ViewBag.Leaves = await _db.Leaves
                    .Include(l => l.User)
                    .Include(l => l.Leave_type)
                    .Include(l => l.Approver1)
                    .Include(l => l.Approver2)
                    .Where(l => l.User.Department_ID == departmentId)
                    .ToListAsync();

            ViewBag.Departments = new SelectList(_db.Departments.ToList(), "Department_ID", "Department_name", departmentId);
            ViewBag.LeaveTypes = new SelectList(_db.Leave_types.ToList(), "Leave_type_ID", "Leave_type_name");

            return View();
        }

        public async Task<IActionResult> Leaves()
        {
            ViewBag.Leaves = await _db.Leaves
                    .Include(l => l.User)
                    .Include(l => l.Leave_type)
                    .Include(l => l.Approver1)
                    .Include(l => l.Approver2)
                    .Where(l => l.Personnel_ID == GetPersonnelID())
                    .ToListAsync();

            ViewBag.LeaveTypes = new SelectList(_db.Leave_types.ToList(), "Leave_type_ID", "Leave_type_name");

            return View();
        }

        public async Task<IActionResult> Add(Leave request)
        {
            var existingLeaves = _db.Leaves
                .Where(l => l.Personnel_ID == request.Personnel_ID && l.Status != "Denied")
                .AsEnumerable()
                .Any(l => Overlaps(l.Date_start, l.Date_end, request.Date_start, request.Date_end));

            if (existingLeaves)
            {
                ModelState.AddModelError("Leave_ID", "Existing leave overlaps for this date.");
                return View("Index", request);
            }

            await Save(request);

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id, [Bind("Leave_ID,Personnel_ID,Leave_type_ID,Status,Date_start,Date_end")] Leave leave)
        {
            if (id != leave.Leave_ID)
            {
                return RedirectToAction(nameof(Leaves));
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingLeaves = _db.Leaves
                        .Where(l => l.Personnel_ID == leave.Personnel_ID && l.Leave_ID != leave.Leave_ID && (l.Status != "Denied" && l.Status != "Cancelled" && l.Status != "Withdrawn"))
                        .AsEnumerable()
                        .Any(l => Overlaps(l.Date_start, l.Date_end, leave.Date_start, leave.Date_end));

                    if (existingLeaves)
                    {
                        ModelState.AddModelError("Leave_ID", "Existing leave overlaps for this date.");
                        return View(nameof(Leaves), leave);
                    }

                    _db.Leaves.Update(leave);
                    await _db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unable to update Leave: {id} - {ex}");
                }

                return RedirectToAction(nameof(Leaves));
            }

            ViewBag.Leaves = await _db.Leaves
                    .Include(l => l.User)
                    .Include(l => l.Leave_type)
                    .Include(l => l.Approver1)
                    .Include(l => l.Approver2)
                    .Where(l => l.Personnel_ID == GetPersonnelID())
                    .ToListAsync();

            ViewBag.LeaveTypes = new SelectList(_db.Leave_types.ToList(), "Leave_type_ID", "Leave_type_name");

            return View(nameof(Leaves), leave);
        }

        public async Task<IActionResult> Apply(Leave request)
        {
            var existingLeaves = _db.Leaves
                .Where(l => l.Personnel_ID == request.Personnel_ID && l.Status != "Denied")
                .AsEnumerable()
                .Any(l => Overlaps(l.Date_start, l.Date_end, request.Date_start, request.Date_end));

            if (existingLeaves)
            {
                return BadRequest("Existing leave overlaps for this date.");
            }

            var personnelId = int.Parse(User.FindFirstValue("Personnelid"));

            request.Personnel_ID = personnelId;
            request.Status = "Pending";

            if (User.IsInRole("manager") || User.IsInRole("topManager"))
            {
                request.Approver_1 = personnelId;
            }

            _db.Leaves.Add(request);
            await _db.SaveChangesAsync();

            var referrerUrl = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referrerUrl))
            {
                // Parse the referrer URL to extract controller information if needed
                // This step assumes the referrer URL is in the standard routing format
                Uri referrerUri = new Uri(referrerUrl);
                var segments = referrerUri.Segments;
                string previousController = segments.Length > 1 ? segments[1].Trim('/') : string.Empty;
                string previousAction = segments.Length > 2 ? segments[2].Trim('/') : string.Empty;

                if (previousAction == "Calendar")
                {
                    return RedirectToAction(previousAction, previousController); // redirect back to the page where request came from
                }
            }

            // Redirect to Schedule page
            return RedirectToAction("Index", "Schedule", new { month = request.Date_start.Month, year = request.Date_start.Year });
        }

        public async Task<IActionResult> Cancel(int Id)
        {
            var leave = await _db.Leaves.FindAsync(Id);
            if (leave == null) return NotFound();

            leave.Status = "Cancelled";
            _db.Leaves.Update(leave);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<bool> Save(Leave request)
        {
            try
            {
                var personnelId = int.Parse(User.FindFirstValue("Personnelid"));

                request.Personnel_ID = personnelId;
                request.Status = "Pending";
                _db.Leaves.Add(request);
                await _db.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        // Check if the dates overlap
        public bool Overlaps(DateTime start1, DateTime end1, DateTime start2, DateTime end2)
        {
            return start1 <= end2 && start2 <= end1;
        }

        [HttpPost]
        public IActionResult GetLeave(int Id)
        {
            var leave = _db.Leaves
                .Include(l => l.Leave_type)
                .Include(l => l.User)
                .Include(l => l.Approver1)
                .Include(l => l.Approver2)
                .FirstOrDefault(l => l.Leave_ID == Id);

            if (leave == null) return NotFound();

            return Ok(new
            {
                Name = leave.User.Full_name,
                Personnel_ID = leave.Personnel_ID,
                Leave_ID = leave.Leave_ID,
                Leave_type_ID = leave?.Leave_type_ID,
                Leave_type_name = leave?.Leave_type?.Leave_type_name,
                Date_start = leave?.Date_start_string,
                Date_end = leave?.Date_end_string,
                Approver_1 = leave?.Approver1?.Full_name,
                Approver_2 = leave?.Approver2?.Full_name,
                Status = leave?.Status,
                Comment = leave?.Comment
            });
        }

        public async Task<IActionResult> Approve(int Id)
        {
            var leave = await _db.Leaves
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Leave_ID == Id);
            if (leave == null) return NotFound();

            if (leave.Approver_1 != null)
            {
                leave.Status = "Approved";
                leave.Date_approved_2 = DateTime.Now.Date;
                leave.Approver_2 = int.Parse(User.FindFirstValue("Personnelid"));
            }
            else
            {
                leave.Date_approved_1 = DateTime.Now.Date;
                leave.Approver_1 = int.Parse(User.FindFirstValue("Personnelid"));
            }

            _db.Leaves.Update(leave);
            await _db.SaveChangesAsync();

            return RedirectToAction("DepartmentLeaves", new { departmentId = leave?.User?.Department_ID });
        }

        public async Task<IActionResult> Deny(int Id, string Comment)
        {
            var leave = await _db.Leaves
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Leave_ID == Id);
            if (leave == null) return NotFound();

            leave.Status = "Denied";
            leave.Comment = Comment;

            if (leave.Approver_1 != null)
            {
                leave.Date_approved_2 = DateTime.Now.Date;
                leave.Approver_2 = int.Parse(User.FindFirstValue("Personnelid"));
            } else
            {
                leave.Date_approved_1 = DateTime.Now.Date;
                leave.Approver_1 = int.Parse(User.FindFirstValue("Personnelid"));
            }

            _db.Leaves.Update(leave);
            await _db.SaveChangesAsync();

            return RedirectToAction("DepartmentLeaves", new { departmentId = leave?.User?.Department_ID });
        }

        public async Task<IActionResult> Approve1(int Id)
        {
            var leave = await _db.Leaves
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Leave_ID == Id);
            if (leave == null) return NotFound();

            if (leave.Approver_1 != null)
            {
                leave.Status = "Approved";
                leave.Date_approved_2 = DateTime.Now.Date;
                leave.Approver_2 = int.Parse(User.FindFirstValue("Personnelid"));
            }
            else
            {
                leave.Date_approved_1 = DateTime.Now.Date;
                leave.Approver_1 = int.Parse(User.FindFirstValue("Personnelid"));
            }

            _db.Leaves.Update(leave);
            await _db.SaveChangesAsync();

            return RedirectToAction("Index", "Schedule", new { month = leave.Date_start.Month, year = leave.Date_start.Year, departmentId = leave?.User?.Department_ID });
        }

        public async Task<IActionResult> Deny1(int Id, string Comment)
        {
            var leave = await _db.Leaves
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Leave_ID == Id);
            if (leave == null) return NotFound();

            leave.Status = "Denied";
            leave.Comment = Comment;

            if (leave.Approver_1 != null)
            {
                leave.Date_approved_2 = DateTime.Now.Date;
                leave.Approver_2 = int.Parse(User.FindFirstValue("Personnelid"));
            }
            else
            {
                leave.Date_approved_1 = DateTime.Now.Date;
                leave.Approver_1 = int.Parse(User.FindFirstValue("Personnelid"));
            }

            _db.Leaves.Update(leave);
            await _db.SaveChangesAsync();

            return RedirectToAction("Index", "Schedule", new { month = leave.Date_start.Month, year = leave.Date_start.Year, departmentId = leave?.User?.Department_ID });
        }

        public int GetPersonnelID()
        {
            var personnelId = int.Parse(User.FindFirstValue("Personnelid"));

            return personnelId;
        }

        public async Task<User> ThisUser()
        {
            return _db.Users.Find(GetPersonnelID());
        }
    }
}
