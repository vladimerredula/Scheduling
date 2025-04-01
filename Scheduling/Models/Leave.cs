using System.ComponentModel.DataAnnotations;

namespace Scheduling.Models
{
    public class Leave
    {
        [Key]
        public int Leave_ID { get; set; }
        public string Leave_name { get; set; }
        public string BsColor { get; set; }
    }
}
