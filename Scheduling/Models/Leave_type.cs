using System.ComponentModel.DataAnnotations;

namespace Scheduling.Models
{
    public class Leave_type
    {
        [Key]
        public int Leave_type_ID { get; set; }

        [Display(Name = "Leave type")]
        public string Leave_type_name { get; set; }
        public string BsColor { get; set; }
    }
}
