using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scheduling.Models
{
	public class Department
	{
		[Key]
		[Display(Name = "Department ID")]
		public int Department_ID { get; set; }

		[Required]
		[Column(TypeName = "VARCHAR")]
        [Display(Name = "Department name")]
        [StringLength(50)]
		public string? Department_name { get; set; }
    }
}
