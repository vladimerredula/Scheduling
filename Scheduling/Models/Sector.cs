using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scheduling.Models
{
	public class Sector
	{
		[Key]
		[Display(Name = "Section ID")]
		public int Sector_ID { get; set; }

		[Required]
		[Column(TypeName = "VARCHAR")]
		[Display(Name = "Sector name")]
		[StringLength(50)]
		public string? Sector_name { get; set; }

		[ForeignKey("Departments")]
		public int? Department_ID { get; set; }
		public virtual Department? Departments { get; set; }

		public int? Order { get; set; }
    }
}
