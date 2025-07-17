using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scheduling.Models
{
    public class Session
    {
        [Key]
        public string Session_ID { get; set; }

        public int Personnel_ID { get; set; }
        public string Ip_address { get; set; }
        public DateTime Last_activity { get; set; }
        public string User_agent { get; set; }
        public string App_name { get; set; }


        [ForeignKey("Personnel_ID")]
        public User User { get; set; }
    }
}
