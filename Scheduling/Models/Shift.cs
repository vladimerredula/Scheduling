using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scheduling.Models
{
    public class Shift
    {
        [Key]
        [Display(Name = "Shift ID")]
        public int Shift_ID { get; set; }

        [Required]
        [Column(TypeName = "VARCHAR")]
        [Display(Name = "Shift name")]
        [StringLength(25)]
        public string? Shift_name { get; set; }
        [Display(Name = "Start time")]
        public TimeSpan? Time_start { get; set; }
        [Display(Name = "End time")]
        public TimeSpan? Time_end { get; set; }
        public string? Pattern { get; set; }
        public int? Department_ID { get; set; }
    }
}
