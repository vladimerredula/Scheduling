using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Scheduling.Models.Templates
{
    public class Template_page
    {
        [Key]
        public int Template_page_ID { get; set; }

        [ForeignKey("Template_module")]
        public int Template_module_ID { get; set; }
        public virtual Template_module? Template_module { get; set; }

        [ForeignKey("Template")]
        public int Template_ID { get; set; }
        public virtual Template? Template { get; set; }

        [ForeignKey("Page")]
        public int Page_ID { get; set; }
        public virtual Page? Page { get; set; }

        public ICollection<Template_component> Components { get; set; } = new List<Template_component>();
    }
}
