using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scheduling.Models.Templates
{
    public class Component
    {
        [Key]
        public int Component_ID { get; set; }

        [Required]
        [Display(Name = "Component name")]
        public string Component_name { get; set; }
        public string Component_abbreviation { get; set; }

        [ForeignKey("Page")]
        public int Page_ID { get; set; }
        public virtual Page? Page { get; set; }
    }
}
