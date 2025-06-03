using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scheduling.Models.Templates
{
    public class Page
    {
        [Key]
        public int Page_ID { get; set; }
        public string? Page_name { get; set; }
        public string? Controller_name { get; set; }
        public string? Action_name { get; set; }
        public string? Route_data { get; set; }

        [ForeignKey("Module")]
        public int Module_ID { get; set; }
        public virtual Module? Module { get; set; }

        public ICollection<Component> Components { get; set; } = new List<Component>();
    }
}
