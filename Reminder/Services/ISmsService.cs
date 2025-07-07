namespace Reminder.Services
{
    public interface ISmsService
    {
        Task<bool> SendVerificationCodeAsync(string phoneNumber, string code);
        Task<bool> SendReminderSmsAsync(string phoneNumber, string reminderMessage, DateTime reminderTime);
        Task<bool> SendWelcomeSmsAsync(string phoneNumber, string name);
    }
} 