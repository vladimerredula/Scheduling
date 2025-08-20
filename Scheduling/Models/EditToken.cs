using System.ComponentModel.DataAnnotations;

namespace Scheduling.Models
{
    public class Edit_token
    {
        [Key]
        public int Id { get; set; }
        public int Department_ID { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public DateTime Expiry { get; set; }
        public bool Local_saved { get; set; } = false;
        public bool Nas_saved { get; set; } = false;
    }
}
