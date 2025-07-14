namespace Reminder.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailVerificationAsync(string email, string name, string token);
        Task<bool> SendPasswordResetAsync(string email, string name, string token);
        Task<bool> SendReminderEmailAsync(string email, string name, string reminderMessage, DateTime reminderTime, int scheduleId);
        Task<bool> SendWelcomeEmailAsync(string email, string name);
    }
} 