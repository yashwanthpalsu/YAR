using Reminder.Models.DBEntities;

namespace Reminder.Services
{
    public interface IReminderService
    {
        /// <summary>
        /// Creates a reminder and schedules notification jobs (email, SMS, WhatsApp) for each schedule.
        /// </summary>
        Task<bool> CreateReminderAsync(ReminderViewModel reminder);
    }
}
