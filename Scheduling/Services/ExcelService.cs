using ClosedXML.Excel;
using Scheduling.Models;

namespace Scheduling.Services
{
    public class ExcelService
    {
        public byte[] Schedule(List<dynamic> users, List<dynamic> schedules, List<Shift> shifts, List<Leave> leaves, List<Holiday> holidays, string department, int month, int year)
        {
            using (var workbook = CreateWorkbook())
            {
                var monthYear = new DateTime(year, month, 1);
                var ws = workbook.Worksheets.Add(monthYear.ToString("MMMM yyyy"));
                ws.Style.Font.FontSize = 12;

                var daysInMonth = DateTime.DaysInMonth(year, month);
                var row = 2;
                var col = 2;

                // Add headers
                var departmentName = ws.Cell(row, col);
                departmentName.SetValue(department)
                    .Style
                    .Font.SetFontSize(14)
                    .Font.SetBold(true);

                col++;

                var monthYearCell = ws.Range($"{Col(col)}{row}:{Col(col + daysInMonth - 1)}{row}");
                monthYearCell.Merge()
                    .SetValue(monthYear.ToString("MMMM yyyy"))
                    .Style
                    .Font.SetBold(true)
                    .Border.SetOutsideBorder(XLBorderStyleValues.Medium)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                row = 4;
                col = 2;

                var nameCol = ws.Range($"{Col(col)}{row}:{Col(col)}{row + 1}");
                nameCol.Merge()
                    .SetValue("Name")
                    .Style
                    .Font.SetBold(true)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Border.SetOutsideBorder(XLBorderStyleValues.Medium);

                for (int day = 1; day <= daysInMonth; day++)
                {
                    var dateCell = ws.Cell(row, day + col);
                    dateCell.SetValue(day)
                        .Style
                        .Font.SetBold(true)
                        .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                        .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                        .Border.SetTopBorder(XLBorderStyleValues.Medium);

                    var date = new DateTime(year, month, day);
                    var dayCell = ws.Cell(row + 1, day + col);
                    dayCell.SetValue(date.ToString("ddd"))
                        .Style
                        .Font.SetFontSize(10)
                        .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                        .Border.SetBottomBorder(XLBorderStyleValues.Medium);

                    if (day == daysInMonth)
                    {
                        dateCell.Style.Border.RightBorder = XLBorderStyleValues.Medium;
                        dayCell.Style.Border.RightBorder = XLBorderStyleValues.Medium;
                    } else
                    {
                        dateCell.Style.Border.RightBorder = XLBorderStyleValues.Thin;
                        dayCell.Style.Border.RightBorder = XLBorderStyleValues.Thin;
                    }

                    var holiday = holidays.FirstOrDefault(h => h.Date == date);
                    bool isHoliday = holiday != null;
                    bool isCompanyHoliday = holiday?.Type == "Company";
                    bool isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

                    if (isHoliday || isWeekend)
                    {
                        if (isCompanyHoliday)
                        {
                            dateCell.Style
                                .Font.SetFontColor(XLColor.White)
                                .Fill.SetBackgroundColor(XLColor.FromHtml("6c757d"));
                            dayCell.Style
                                .Font.SetFontColor(XLColor.White)
                                .Fill.SetBackgroundColor(XLColor.FromHtml("6c757d"));
                        } else
                        {
                            dateCell.Style.Fill.BackgroundColor = XLColor.FromHtml("e2e3e5");
                            dayCell.Style.Fill.BackgroundColor = XLColor.FromHtml("e2e3e5");
                        }
                    }
                }

                row += 2;

                // Add user data
                foreach (var sector in users.GroupBy(u => u.Sector_ID))
                {
                    foreach (var user in sector)
                    {
                        ws.Cell(row, col).Value = user.Full_name;

                        for (int day = 1; day <= daysInMonth; day++)
                        {
                            var date = new DateTime(year, month, day);
                            bool isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

                            var schedule = schedules.FirstOrDefault(sc => sc.Date == date && sc.Personnel_ID == user.Personnel_ID);
                            var shift = schedule?.Shift ?? string.Empty;
                            var shiftCell = ws.Cell(row, col + day);

                            var holiday = holidays.FirstOrDefault(h => h.Date == date);
                            bool isHoliday = holiday != null;
                            bool isCompanyHoliday = holiday?.Type == "Company";

                            var leave = leaves.FirstOrDefault(l => l.Personnel_ID == user.Personnel_ID && date >= l.Date_start && date <= l.Date_end);
                            bool hasLeave = leave != null && leave?.Status == "Approved";

                            if (isHoliday || isWeekend)
                            {
                                if (isCompanyHoliday)
                                {
                                    shiftCell.Style.Fill.BackgroundColor = XLColor.FromHtml("6c757d");
                                    shift = string.Empty;
                                }
                                else
                                {
                                    shiftCell.Style.Fill.BackgroundColor = XLColor.FromHtml("e2e3e5");
                                }
                            }

                            if (hasLeave && !isCompanyHoliday)
                            {
                                if (!string.IsNullOrEmpty(shift))
                                {
                                    shiftCell.Style.Fill.BackgroundColor = XLColor.FromHtml(GetLeaveBgColor(leave.Leave_type_ID));
                                    shift = string.Empty;
                                }
                            }

                            shiftCell.Value = shift;

                            if (schedule?.Comment == "cancelled" && !string.IsNullOrEmpty(shift))
                            {
                                shiftCell.Style
                                    .Border.SetDiagonalBorder(XLBorderStyleValues.Thin)
                                    .Border.SetDiagonalUp(true)
                                    .Border.SetDiagonalDown(true)
                                    .Border.SetDiagonalBorderColor(XLColor.Red);
                            }
                        }

                        row++;
                    }
                }

                var empCount = users.Count() - 1;

                // Shift counter
                var genShifts = new[] { "A", "B", "C" };
                foreach (var shiftName in genShifts)
                {
                    ws.Cell(row, col).Value = $"Shift {shiftName}";
                    ws.Cell(row, col).Style.Font.SetBold(true);
                    ws.Cell(row, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                    for (int day = 1; day <= daysInMonth; day++)
                    {
                        ws.Cell(row, col + day).FormulaA1 = $"=COUNTIF({Col(col + day)}$6:{Col(col + day)}${6 + empCount},\"{shiftName}\")";
                    }

                    var shiftCountRange = ws.Range($"{Col(col + 1)}{row}:{Col(col + daysInMonth)}{row}");
                    shiftCountRange.Style
                        .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                        .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    shiftCountRange.AddConditionalFormat().WhenEquals(0)
                        .Fill.SetBackgroundColor(XLColor.FromHtml("f8d7da"))
                        .Font.SetFontColor(XLColor.DarkRed);
                    shiftCountRange.AddConditionalFormat().WhenEquals(1)
                        .Fill.SetBackgroundColor(XLColor.FromHtml("fff3cd"))
                        .Font.SetFontColor(XLColor.FromHtml("6C5F00"));
                    shiftCountRange.AddConditionalFormat().WhenEquals(2)
                        .Fill.SetBackgroundColor(XLColor.FromHtml("d1e7dd"))
                        .Font.SetFontColor(XLColor.DarkOliveGreen);
                    shiftCountRange.AddConditionalFormat().WhenGreaterThan(2)
                        .Fill.SetBackgroundColor(XLColor.FromHtml("198754"))
                        .Font.SetFontColor(XLColor.White);

                    row++;
                }

                ws.Range($"{Col(col + 1)}{row - 3}:{Col(col + daysInMonth)}{row - 1}").Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);

                row = 6;

                // Styles
                var empTable = ws.Range($"{Col(col)}{row}:{Col(daysInMonth + 2)}{row + empCount}");
                empTable.Style
                    .Border.SetOutsideBorder(XLBorderStyleValues.Medium)
                    .Border.SetInsideBorder(XLBorderStyleValues.Thin)
                    .Border.SetInsideBorderColor(XLColor.FromHtml("808080"))
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center);

                var row1 = row;
                foreach (var sector in users.GroupBy(u => u.Sector_ID))
                {
                    ws.Range($"{Col(col)}{row1}:{Col(col + daysInMonth)}{row1 + sector.Count() - 1}").Style
                        .Border.SetOutsideBorder(XLBorderStyleValues.Medium)
                        .Border.SetOutsideBorderColor(XLColor.Black);
                    row1 += sector.Count();
                }

                var empNames = ws.Range($"{Col(col)}{row}:{Col(col)}{row + empCount}");
                empNames.Style
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left)
                    .Border.SetOutsideBorder(XLBorderStyleValues.Medium);

                // Auto-fit columns
                ws.Rows().Height = 21;
                ws.Row(5).Height = 13.5;
                ws.Columns().AdjustToContents();
                for (int day = 1; day <= daysInMonth; day++)
                {
                    ws.Column(col + day).Width = 4.6; // set your desired width
                }

                // Legends
                row = 6;
                col = daysInMonth + 4;
                ws.Column(col).Width = 4.6;
                ws.Column(col + 1).Width = 20;

                ws.Cell(row, col).Style.Fill.SetBackgroundColor(XLColor.FromHtml(GetLeaveBgColor(10)));
                ws.Cell(row, col + 1).SetValue(" Business Trip")
                    .Style
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);

                row += 2;

                ws.Cell(row, col).Style.Fill.SetBackgroundColor(XLColor.FromHtml(GetLeaveBgColor(11)));
                ws.Cell(row, col + 1).SetValue(" Paid Leave")
                    .Style
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);

                row += 2;

                ws.Cell(row, col).Style.Fill.SetBackgroundColor(XLColor.FromHtml(GetLeaveBgColor(12)));
                ws.Cell(row, col + 1).SetValue(" Unpaid Leave")
                    .Style
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);

                row += 2;

                ws.Cell(row, col).Style.Fill.SetBackgroundColor(XLColor.FromHtml("6c757d"));
                ws.Cell(row, col + 1).SetValue(" Company Holiday")
                    .Style
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);

                row += 3;

                foreach (var pattern in new[] { "4/2", "5/2" })
                {
                    var shiftPatterns = shifts.Where(s => s.Pattern == pattern).ToList();
                    if (shiftPatterns.Any())
                    {

                        ws.Range($"{Col(col)}{row}:{Col(col + 1)}{row}")
                            .Merge().SetValue($"{pattern} Pattern")
                            .Style.Font.SetBold(true)
                            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                            .Border.SetOutsideBorder(XLBorderStyleValues.Medium);

                        row++;

                        foreach (var shift in shiftPatterns)
                        {
                            ws.Cell(row, col).Value = shift.Shift_name;
                            ws.Cell(row, col + 1).Value = $"{shift?.Time_start?.ToString(@"hh\:mm")} - {shift?.Time_end?.ToString(@"hh\:mm")}";
                            row++;
                        }

                        ws.Range($"{Col(col)}{row - shiftPatterns.Count()}:{Col(col + 1)}{row - 1}")
                            .Style.Border.SetInsideBorder(XLBorderStyleValues.Thin)
                            .Border.SetOutsideBorder(XLBorderStyleValues.Medium)
                            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                        row++;
                    }
                }

                // Convert the Excel workbook to a byte array and return
                using (var stream = new System.IO.MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        public static XLWorkbook CreateWorkbook()
        {
            var workbook = new XLWorkbook();
            workbook.Properties.Author = "Scheduling WebApp";
            return workbook;
        }


        public static string Col(int number)
        {
            var column = string.Empty;

            while (number > 0)
            {
                number--; // Adjust for 0-based index
                column = (char)('A' + (number % 26)) + column;
                number /= 26;
            }

            return column;
        }

        public static string GetLeaveBgColor(int leaveId)
        {
            switch (leaveId)
            {
                case 10:
                    return "ffc107";
                case 11:
                    return "0DCAF0";
                case 12:
                    return "cff4fc";
                default:
                    return string.Empty;
            }
        }
    }
}
