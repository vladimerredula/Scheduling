using Scheduling.Models.Templates;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scheduling.Models
{
    public class User
    {
        [Key]
        [Display(Name = "Personnel ID")]
        public int Personnel_ID { get; set; }

        [Required]
        [Column(TypeName = "VARCHAR")]
        [Display(Name = "Username")]
        [StringLength(20)]
        public string Username { get; set; }

        [Required]
        [Column(TypeName = "VARCHAR")]
        [Display(Name = "Password")]
        [StringLength(20)]
        public string Password { get; set; }

        [Column(TypeName = "VARCHAR")]
        [Display(Name = "First name")]
        [StringLength(50)]
        public string? First_name { get; set; }

        [Column(TypeName = "VARCHAR")]
        [Display(Name = "Last name")]
        [StringLength(50)]
        public string? Last_name { get; set; }

        [Display(Name = "Privilege")]
        [ForeignKey("Privileges")]
        public int? Privilege_ID { get; set; }

        [Display(Name = "Department")]
        [ForeignKey("Departments")]
        public int? Department_ID { get; set; }

        [Display(Name = "Team")]
        [ForeignKey("Sector")]
        public int? Sector_ID { get; set; }
        public virtual Sector? Sector { get; set; }
        public DateTime? Date_hired { get; set; }
        public DateTime? Last_day { get; set; }

        public int Status { get; set; } = 0;
        public DateTime? Last_password_changed { get; set; }

        [ForeignKey("Template")]
        public int? Template_ID { get; set; }
        public virtual Template? Template { get; set; }

        [Display(Name = "Name")]
        public string? Full_name
        {
            get
            {
                if (First_name != null && Last_name != null)
                    return First_name + " " + Last_name;
                else
                    return null;
            }
        }
    }
}
