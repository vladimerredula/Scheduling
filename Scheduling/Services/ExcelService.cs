using ClosedXML.Excel;
using Scheduling.Models;

namespace Scheduling.Services
{
    public class ExcelService
    {
        public byte[] Schedule(List<dynamic> users, List<dynamic> schedules, List<Leave> leaves, List<Holiday> holidays, int month, int year)
        {
            using (var workbook = CreateWorkbook())
            {
                var monthYear = new DateTime(year, month, 1);
                var worksheet = workbook.Worksheets.Add(monthYear.ToString("MMMM yyyy"));

                // Add headers
                worksheet.Cell(1, 1).Value = "Name";
                var daysInMonth = DateTime.DaysInMonth(year, month);
                for (int day = 1; day <= daysInMonth; day++)
                {
                    worksheet.Cell(1, day + 1).Value = day;
                    worksheet.Cell(1, day + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                // Add user data
                int row = 2;
                foreach (var user in users)
                {
                    worksheet.Cell(row, 1).Value = user.Full_name;

                    for (int day = 1; day <= daysInMonth; day++)
                    {
                        var date = new DateTime(year, month, day);
                        var schedule = schedules.FirstOrDefault(sc => sc.Date == date && sc.Personnel_ID == user.Personnel_ID);

                        worksheet.Cell(row, day + 1).Value = schedule?.Shift ?? "";
                    }

                    row++;
                }

                // Auto-fit columns
                worksheet.Columns().AdjustToContents();

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
    }
}
