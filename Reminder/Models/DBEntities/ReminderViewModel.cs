using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Reminder.Models.DBEntities
{
    [Table(name:"Reminder")]
    public class ReminderViewModel
    {
        [Key]
        public int ReminderId { get; set; }
        
        // This will be set automatically from the current user
        [Required(AllowEmptyStrings = false, ErrorMessage = "")]
        public string UserId { get; set; } = string.Empty;

        [Column(TypeName = "varchar(250)")]
        public required string Name { get; set; }

        public bool IsEmailModeSelected { get; set; } = false;
        public bool IsTextModeSelected { get; set; } = false;
        public bool IsCallModeSelected { get; set; } = false;

        [Column(TypeName = "varchar(250)")]
        public required string Message { get; set; }
        public string? ImportanceLevel { get; set; }

        // Navigation property for one-to-many relationship
        public virtual ICollection<ScheduleViewModel> Schedules { get; set; } = new List<ScheduleViewModel>();

        // Helper methods
        public void AddSchedule(DateTime date, TimeSpan time)
        {
            Schedules.Add(new ScheduleViewModel
            {
                Date = date,
                Time = time,
                ReminderId = ReminderId
            });
        }

        public bool HasActiveSchedules()
        {
            return Schedules.Any(s => !s.IsReminderSent);
        }

        public IEnumerable<ScheduleViewModel> GetPendingSchedules()
        {
            return Schedules.Where(s => !s.IsReminderSent);
        }
    }
}
