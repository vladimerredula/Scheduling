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

        [Display(Name = "Day type")]
        public string Day_type { get; set; }
        public string? Message { get; set; } // Leave request message
        public string? Comment { get; set; } // Leave approval comment
        public string? Status { get; set; } // "Pending", "Approved", "Denied", "Cancelled"

        [Display(Name = "Date approved (Manager)")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? Date_approved_1 { get; set; }

        [Display(Name = "Date approved (HR)")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? Date_approved_2 { get; set; }

        [ForeignKey("User")]
        public int Personnel_ID { get; set; }
        public User? User { get; set; }

        [ForeignKey("Approver1")]
        [Display(Name = "Approver (Manager)")]
        public int? Approver_1 { get; set; }
        public User? Approver1 { get; set; }

        [ForeignKey("Approver2")]
        [Display(Name = "Approver (HR)")]
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
