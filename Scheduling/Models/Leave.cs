using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scheduling.Models
{
    public class Leave
    {
        [Key]
        public int Leave_ID { get; set; }
        public DateTime Date_start { get; set; }
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
        public int Leave_type_ID { get; set; }
        public Leave_type? Leave_type { get; set; }
    }
}
