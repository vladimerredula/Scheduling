using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scheduling.Models
{
    public class Leave
    {
        [Key]
        public int Leave_ID { get; set; }

        [Display(Name = "Date start")]
        public DateTime Date_start { get; set; }

        [Display(Name = "Date end")]
        public DateTime Date_end { get; set; }
        public string? Comment { get; set; }
        public string? Status { get; set; } // "Pending", "Approved", "Denied", "Reflected", "Cancelled"
        public DateTime? Date_approved { get; set; }
        public DateTime? Date_reflected { get; set; }

        [ForeignKey("User")]
        public int Personnel_ID { get; set; }
        public User? User { get; set; }

        [ForeignKey("Approver")]
        public int? Approved_by { get; set; }
        public User? Approver { get; set; }

        [ForeignKey("Leave_type")]
        [Display(Name = "Leave type")]
        public int Leave_type_ID { get; set; }
        public Leave_type? Leave_type { get; set; }

        public string? Date_start_string
        {
            get
            {
                if (Date_start != null && Date_start != DateTime.MinValue)
                    return Date_start.ToString("yyyy-MM-dd");
                else
                    return null;
            }
        }
        public string? Date_end_string
        {
            get
            {
                if (Date_end != null && Date_end != DateTime.MinValue)
                    return Date_end.ToString("yyyy-MM-dd");
                else
                    return null;
            }
        }
    }
}
