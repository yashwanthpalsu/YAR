using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Reminder.Models.DBEntities
{
    [Table(name:"Schedule")]
    public class ScheduleViewModel
    {
        [Key]
        public int ScheduleId { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan Time { get; set; }
        public bool IsReminderSent { get; set; } = false;
        public bool IsAcknowledged { get; set; } = false;
        
        [ForeignKey("Reminder")]
        public int ReminderId { get; set; }
        
        public ReminderViewModel? Reminder { get; set; }
    }
}
