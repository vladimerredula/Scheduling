using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Scheduling.Models.Templates
{
    public class Template_component
    {
        [Key]
        public int Template_component_ID { get; set; }

        [ForeignKey("Template_page")]
        public int Template_page_ID { get; set; }
        public virtual Template_page? Template_page { get; set; }

        [ForeignKey("Template")]
        public int Template_ID { get; set; }
        public virtual Template? Template { get; set; }

        [ForeignKey("Component")]
        public int Component_ID { get; set; }
        public virtual Component? Component { get; set; }
    }
}
