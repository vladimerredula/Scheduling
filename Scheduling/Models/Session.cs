using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scheduling.Models
{
    public class Session
    {
        [Key]
        public string Session_ID { get; set; } = Guid.NewGuid().ToString();

        public int Personnel_ID { get; set; }

        public string Ip_address { get; set; } = string.Empty;

        public string User_agent { get; set; } = string.Empty;

        public string App_name { get; set; } = "SCH";

        public DateTime? Signed_in_at { get; set; } = DateTime.Now;

        public DateTime? Signed_out_at { get; set; }

        public DateTime Last_activity { get; set; } = DateTime.Now;

        public bool IsActive => Signed_out_at == null;


        [ForeignKey("Personnel_ID")]
        public User User { get; set; }
    }
}
