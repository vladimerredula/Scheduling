using System.ComponentModel.DataAnnotations;

namespace Scheduling.Models
{
    public class Shift
    {
        [Key]
        public int Shift_ID { get; set; }
        public string Shift_name { get; set; } // Shift A, B, C
        public TimeSpan Time_start { get; set; }
        public TimeSpan Time_end { get; set; }
    }
}
