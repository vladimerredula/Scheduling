﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scheduling.Models
{
    public class Schedule
    {
        [Key]
        public int Schedule_ID { get; set; }
        public int Personnel_ID { get; set; }
        public int? Shift_ID { get; set; }
        public string? Comment { get; set; }
        public DateTime Date { get; set; }

        [ForeignKey("Personnel_ID")]
        public User? User { get; set; }

        [ForeignKey("Shift_ID")]
        public Shift? Shift { get; set; }
    }
}
