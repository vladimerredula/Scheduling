using System.ComponentModel.DataAnnotations;

namespace Scheduling.Models
{
    public class Holiday
    {
        [Key]
        public int Holiday_ID { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public string? Name { get; set; }

        [Required]
        [Display(Name = "Holiday type")]
        public string? Type { get; set; } // "Regular" or "Company"
    }
}
