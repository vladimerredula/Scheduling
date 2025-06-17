using Scheduling.Models;
using Scheduling.Services;

namespace Scheduling.Helpers
{
    public class ScheduleHelper
    {
        private readonly UserService _user;

        public ScheduleHelper(UserService user)
        {
            _user = user;
        }

        // Get all users in the department
        public async Task<List<User>> GetBaseUsers(int month, int year, int? departmentId = null)
        {
            if (departmentId == null || departmentId == 0)
                departmentId = await _user.GetDepartmentId();

            var users = await _user.GetUsers(
                status: 1,
                activeOnly: true,
                departmentId: departmentId);

            return users;
        }

        // Sort users by schedule order and fallback to sector/order/name
        public List<User> SortUsers(
            List<User> users,
            List<Schedule_order> scheduleOrder)
        {
            var usersInOrder = users
                .Where(u => scheduleOrder.Any(o => o.Personnel_ID == u.Personnel_ID))
                .OrderBy(u => scheduleOrder.First(o => o.Personnel_ID == u.Personnel_ID).Order_index);

            var usersNotInOrder = users
                .Where(u => scheduleOrder.All(o => o.Personnel_ID != u.Personnel_ID))
                .OrderBy(u => u?.Sector?.Order)
                .ThenByDescending(u => u.Privilege_ID)
                .ThenBy(u => u.First_name)
                .ThenBy(u => u.Last_name);

            return usersInOrder.Concat(usersNotInOrder).ToList();
        }

        // Override Sector_IDs and Privilege_IDs
        public List<User> OverrideSectorAndPrivilege(
            List<User> users,
            List<Schedule_order> scheduleOrder,
            List<Schedule_shiftleader> scheduleShiftleaders)
        {
            foreach (var baseUser in users)
            {
                var matchOrder = scheduleOrder
                    .FirstOrDefault(o => o.Personnel_ID == baseUser.Personnel_ID);

                if (matchOrder != null && matchOrder.Sector_ID.HasValue)
                    baseUser.Sector_ID = matchOrder.Sector_ID.Value;

                var matchShiftleaders = scheduleShiftleaders
                    .FirstOrDefault(o => o.Personnel_ID == baseUser.Personnel_ID);

                if (matchShiftleaders != null && matchShiftleaders.Is_shiftleader.HasValue)
                    baseUser.Privilege_ID = baseUser.Privilege_ID != 3 
                        ? matchShiftleaders.Is_shiftleader.Value 
                            ? 2 : 1 
                        : baseUser.Privilege_ID;
            }

            return SortUsers(users, scheduleOrder);
        }


        // Get shift leaders for each shift per day
        public List<(DateTime, int, string)> CalculateShiftLeaders(
            List<Schedule> schedules,
            List<Schedule_shiftleader> scheduleShiftLeaders,
            List<Leave> leaves)
        {
            var shiftLeaders = new List<(DateTime, int, string)>();
            var shiftLeaderIds = GetShiftLeaderIds(scheduleShiftLeaders);

            if (schedules == null || schedules.Count == 0)
                return shiftLeaders;

            var groupedByDate = schedules
                .Where(s => new[] { "A", "B", "C" }.Contains(s.Shift?.Shift_name))
                .GroupBy(s => s.Date);

            foreach (var group in groupedByDate)
            {
                var date = group.Key;

                var shiftGroups = group.GroupBy(d => d.Shift?.Shift_name);

                foreach (var shiftGroup in shiftGroups)
                {
                    var shiftName = shiftGroup.Key;
                    var explicitLeader = shiftGroup
                        .FirstOrDefault(s => s.Is_shiftleader == true);

                    if (explicitLeader != null &&
                        !IsUserOnLeave(explicitLeader.Personnel_ID, date, leaves))
                    {
                        shiftLeaders.Add((date, explicitLeader.Personnel_ID, shiftName));
                        continue;
                    }

                    var defaultLeaders = shiftGroup
                        .Where(s => shiftLeaderIds.Contains(s.Personnel_ID))
                        .ToList();

                    if (defaultLeaders.Count == 1)
                    {
                        var fallbackLeader = defaultLeaders.FirstOrDefault();
                        if (!IsUserOnLeave(fallbackLeader.Personnel_ID, date, leaves))
                        {
                            shiftLeaders.Add((date, fallbackLeader.Personnel_ID, shiftName));
                        }
                    }
                }
            }

            return shiftLeaders;
        }

        public static HashSet<int?> GetShiftLeaderIds(List<Schedule_shiftleader> shiftLeaders)
        {
            return shiftLeaders
                .Where(o => o.Is_shiftleader == true)
                .Select(o => o.Personnel_ID)
                .ToHashSet();
        }

        public static bool IsUserOnLeave(int userId, DateTime date, List<Leave> leaves)
        {
            return leaves.Any(l => l.Personnel_ID == userId &&
                                   date >= l.Date_start &&
                                   date <= l.Date_end &&
                                   l.Status == "Approved");
        }

        public string DisplayMonthYear(int month, int year)
        {
            return new DateTime(year, month, 1).ToString("MMMM yyyy");
        }

        public string ToDateStr(DateTime date)
        {
            return date.ToString("yyyy-MM-dd");
        }
    }
}
