using System.ComponentModel.DataAnnotations;

namespace Reminder.Models.DBEntities
{
    public class ChatMessage
    {
        public int Id { get; set; }
        
        [Required]
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        
        [Required]
        public string Message { get; set; }
        
        [Required]
        public string Response { get; set; }
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        public bool IsUserMessage { get; set; } = true;
        
        public string? Intent { get; set; } // e.g., "schedule_reminder", "list_reminders", "delete_reminder"
        
        public string? ExtractedData { get; set; } // JSON data extracted from user input
    }
} 