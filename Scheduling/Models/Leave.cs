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
        public string? Status { get; set; } // "Pending", "Approved", "Denied", "Cancelled"
        public DateTime? Date_approved_1 { get; set; }
        public DateTime? Date_approved_2 { get; set; }

        [ForeignKey("User")]
        public int Personnel_ID { get; set; }
        public User? User { get; set; }

        [ForeignKey("Approver1")]
        public int? Approver_1 { get; set; }
        public User? Approver1 { get; set; }
        [ForeignKey("Approver2")]
        public int? Approver_2 { get; set; }
        public User? Approver2 { get; set; }

        [ForeignKey("Leave_type")]
        [Display(Name = "Leave type")]
        public int Leave_type_ID { get; set; }
        public Leave_type? Leave_type { get; set; }
        public int? Notify { get; set; } = 0; // 0 - no notification, 1 - new notification sent, 2 - notification, 3 - notification read

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
