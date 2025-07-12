using Reminder.Models.DBEntities;

namespace Reminder.Services
{
    public interface IChatService
    {
        /// <summary>
        /// Process user input and generate AI response
        /// </summary>
        Task<ChatResponse> ProcessUserInputAsync(string userId, string userInput);
    }

    public class ChatResponse
    {
        public string Message { get; set; } = string.Empty;
        public string Intent { get; set; } = string.Empty;
        public bool RequiresFollowUp { get; set; } = false;
        public string? FollowUpQuestion { get; set; }
        public object? ExtractedData { get; set; }
        public bool Success { get; set; } = true;
    }
} 