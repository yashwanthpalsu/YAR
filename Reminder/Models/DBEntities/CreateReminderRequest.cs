using System.ComponentModel.DataAnnotations;

namespace Reminder.Models.DBEntities
{
    public class CreateReminderRequest
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        [StringLength(250)]
        public string Name { get; set; } = string.Empty;

        public bool IsEmailModeSelected { get; set; } = false;
        public bool IsTextModeSelected { get; set; } = false;
        public bool IsCallModeSelected { get; set; } = false;

        [Required]
        [StringLength(250)]
        public string Message { get; set; } = string.Empty;
        
        public string? ImportanceLevel { get; set; }

        public List<CreateScheduleRequest> Schedules { get; set; } = new List<CreateScheduleRequest>();
    }

    public class CreateScheduleRequest
    {
        [Required]
        public DateTime Date { get; set; }

        [Required]
        public TimeSpan Time { get; set; }
    }
} 