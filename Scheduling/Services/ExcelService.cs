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

                // Set landscape orientation and print settings
                ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
                ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
                ws.PageSetup.Margins.Top = 0.5;
                ws.PageSetup.Margins.Bottom = 0.5;
                ws.PageSetup.Margins.Left = 0.5;
                ws.PageSetup.Margins.Right = 0.5;

                ws.PageSetup.FitToPages(1, 1);

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

                col += 2;

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

                var nameCol = ws.Range($"{Col(col)}{row}:{Col(col + 1)}{row + 1}");
                nameCol.Merge()
                    .SetValue("Name")
                    .Style
                    .Font.SetBold(true)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Border.SetOutsideBorder(XLBorderStyleValues.Medium);

                col++;

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
                col = 2;

                var empCount = users.Count() + users.Where(u => u.Employment_type == "PartTime").Count() - 1;


                // Styles
                var empTable = ws.Range($"{Col(col)}{row}:{Col(daysInMonth + 3)}{row + empCount}");
                empTable.Style
                    .Border.SetOutsideBorder(XLBorderStyleValues.Medium)
                    .Border.SetInsideBorder(XLBorderStyleValues.Thin)
                    .Border.SetInsideBorderColor(XLColor.FromHtml("808080"))
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center);

                var row1 = row;
                foreach (var sector in users.GroupBy(u => u.Sector_ID))
                {
                    var partTimers = sector.Where(u => u.Employment_type == "PartTime").Count();
                    ws.Range($"{Col(col)}{row1}:{Col(col + daysInMonth + 1)}{row1 + sector.Count() + partTimers - 1}").Style
                        .Border.SetOutsideBorder(XLBorderStyleValues.Medium)
                        .Border.SetOutsideBorderColor(XLColor.Black);
                    row1 += sector.Count() + partTimers;
                }

                // Add user data
                foreach (var sector in users.GroupBy(u => u.Sector_ID))
                {
                    foreach (var user in sector)
                    {
                        ws.Cell(row, col).Value = user.Full_name;

                        var isPartTime = user.Employment_type == "PartTime";
                        if (isPartTime)
                        {
                            ws.Range($"{Col(col)}{row}:{Col(col)}{row + 1}").Merge();
                            ws.Cell(row, col + 1).Value = "In";
                            ws.Cell(row + 1, col + 1).Value = "Out";

                            ws.Range($"{Col(col + 1)}{row}:{Col(col + 1)}{row + 1}").Style.Border.SetInsideBorder(XLBorderStyleValues.Dotted);
                        } else
                        {
                            ws.Range($"{Col(col)}{row}:{Col(col + 1)}{row}").Merge();
                        }

                        var col1 = col + 1;

                        for (int day = 1; day <= daysInMonth; day++)
                        {
                            var date = new DateTime(year, month, day);
                            bool isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

                            var schedule = schedules.FirstOrDefault(sc => sc.Date == date && sc.Personnel_ID == user.Personnel_ID);

                            var holiday = holidays.FirstOrDefault(h => h.Date == date);
                            bool isHoliday = holiday != null;
                            bool isCompanyHoliday = holiday?.Type == "Company";

                            if (isPartTime)
                            {
                                var timeInCell = ws.Cell(row, col1 + day);
                                var timeOutCell = ws.Cell(row + 1, col1 + day);

                                timeInCell.Value = schedule?.Time_in != null
                                    ? schedule.Time_in.ToString(@"hh\:mm")
                                    : string.Empty;

                                timeOutCell.Value = schedule?.Time_out != null
                                    ? schedule.Time_out.ToString(@"hh\:mm")
                                    : string.Empty;

                                timeInCell.Style.Font.SetFontSize(11);
                                timeOutCell.Style.Font.SetFontSize(11);

                                ws.Range($"{Col(col1 + day)}{row}:{Col(col1 + day)}{row + 1}").Style.Border.SetInsideBorder(XLBorderStyleValues.Dotted);

                                if (isHoliday || isWeekend)
                                {
                                    if (isCompanyHoliday)
                                    {
                                        timeInCell.Style.Fill.BackgroundColor = XLColor.FromHtml("6c757d");
                                        timeOutCell.Style.Fill.BackgroundColor = XLColor.FromHtml("6c757d");
                                        ws.Range($"{Col(col1 + day)}{row}:{Col(col1 + day)}{row1 + 1}").Style.Border.SetInsideBorder(XLBorderStyleValues.None);
                                    }
                                    else
                                    {
                                        timeInCell.Style.Fill.BackgroundColor = XLColor.FromHtml("e2e3e5");
                                        timeOutCell.Style.Fill.BackgroundColor = XLColor.FromHtml("e2e3e5");
                                    }
                                }
                            } else
                            {
                                var shift = schedule?.Shift ?? string.Empty;
                                //shift = shift.Length > 5
                                //    ? shift.Substring(0, 5)
                                //    : shift;
                                var shiftCell = ws.Cell(row, col1 + day);

                                var leave = leaves.FirstOrDefault(l => l.Personnel_ID == user.Personnel_ID && date >= l.Date_start && date <= l.Date_end);
                                bool hasLeave = leave != null;

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
                        }

                        if (isPartTime)
                        {
                            row += 2;
                        } else
                        {
                            row++;
                        }
                    }
                }

                // Shift counter
                if (new List<string> { "HTS Production Department", "IBAD Department", "Chemical Department" }.Contains(department))
                {
                    ws.Range($"{Col(col)}{row}:{Col(col)}{row + 2}").Merge();
                    ws.Range($"{Col(col)}{row}:{Col(col)}{row + 2}").Value = "Shift count";
                    ws.Range($"{Col(col)}{row}:{Col(col)}{row + 2}").Style
                        .Font.SetBold(true)
                        .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                        .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                    var genShifts = new[] { "A", "B", "C" };
                    foreach (var shiftName in genShifts)
                    {
                        var col1 = col + 1;

                        var shiftNameCell = ws.Cell(row, col1);
                        shiftNameCell.Value = shiftName;
                        shiftNameCell.Style
                            .Font.SetBold(true)
                            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right)
                            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);

                        for (int day = 1; day <= daysInMonth; day++)
                        {
                            ws.Cell(row, col1 + day).FormulaA1 = $"=COUNTIF({Col(col1 + day)}$6:{Col(col1 + day)}${6 + empCount},\"{shiftName}\")";
                        }

                        col1++;

                        var shiftCountRange = ws.Range($"{Col(col1)}{row}:{Col(col1 + daysInMonth - 1)}{row}");
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

                    // Shift counter borders
                    ws.Range($"{Col(col)}{row - 3}:{Col(col + daysInMonth + 1)}{row - 1}").Style
                        .Border.SetInsideBorder(XLBorderStyleValues.Thin)
                        .Border.SetOutsideBorder(XLBorderStyleValues.Medium);
                }

                row = 6;

                // Name alignment
                var empNames = ws.Range($"{Col(col)}{row}:{Col(col)}{row + empCount}");
                empNames.Style
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left)
                    .Border.SetLeftBorder(XLBorderStyleValues.Medium);

                ws.Range($"{Col(col + 1)}{row}:{Col(col + 1)}{row + empCount + 3}").Style
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center);

                // Auto-fit columns
                ws.Rows().Height = 21;
                ws.Row(5).Height = 13.5;
                ws.Columns().AdjustToContents();
                for (int day = 1; day <= daysInMonth + 1; day++)
                {
                    ws.Column(col + day).Width = 5.5;
                }

                // Legends
                row = 6;
                col = daysInMonth + 5;
                ws.Column(col).Width = 5.5;
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
                            //ws.Cell(row, col).Value = shift.Shift_name.Length > 5
                            //    ? shift.Shift_name.Substring(0, 5)
                            //    : shift.Shift_name;
                            ws.Cell(row, col).Value = $"{shift.Shift_name} " + (!string.IsNullOrEmpty(shift.Acronym) ? $"({shift.Acronym})" : "");
                            ws.Cell(row, col).Style
                                .Font.SetBold(true)
                                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                            ws.Cell(row, col + 1).Value = $"{shift?.Time_start?.ToString(@"hh\:mm")} - {shift?.Time_end?.ToString(@"hh\:mm")}";
                            ws.Cell(row, col + 1).Style
                                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                            row++;
                        }

                        ws.Range($"{Col(col)}{row - shiftPatterns.Count()}:{Col(col + 1)}{row - 1}")
                            .Style.Border.SetInsideBorder(XLBorderStyleValues.Thin)
                            .Border.SetOutsideBorder(XLBorderStyleValues.Medium)
                            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);

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
