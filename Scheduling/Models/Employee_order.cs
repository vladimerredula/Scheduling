using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scheduling.Models
{
    public class Employee_order
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("User")]
        public int? Personnel_ID { get; set; }
        public User? User { get; set; }

        public int? Order_index { get; set; } // Position in the calendar
        public int? Sector_ID { get; set; } // Team ID
        public int? Year { get; set; }
        public int? Month { get; set; }
    }
}
