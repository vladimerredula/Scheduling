using System.ComponentModel.DataAnnotations;

namespace Scheduling.Models.Templates
{
    public class Template
    {
        [Key]
        public int Template_ID { get; set; }

        [Required]
        [Display(Name = "Template name")]
        public string Template_name { get; set; }
        public string App_name { get; set; }

        public ICollection<Template_module> Modules { get; set; } = new List<Template_module>();
    }
}
