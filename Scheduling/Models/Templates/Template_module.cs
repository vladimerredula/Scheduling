using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scheduling.Models.Templates
{
    public class Template_module
    {
        [Key]
        public int Template_module_ID { get; set; }

        [ForeignKey("Template")]
        public int Template_ID { get; set; }
        public virtual Template? Template { get; set; }

        [ForeignKey("Module")]
        public int Module_ID { get; set; }
        public virtual Module? Module { get; set; }

        public ICollection<Template_page> Pages { get; set; } = new List<Template_page>();
    }
}
