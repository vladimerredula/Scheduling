using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Scheduling.Models
{
    public class Schedule_shiftleader
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("User")]
        public int? Personnel_ID { get; set; }
        public User? User { get; set; }
        public bool? Is_shiftleader { get; set; } // Team ID
        public int? Department_ID { get; set; }
        public int? Year { get; set; }
        public int? Month { get; set; }
    }
}
