using Reminder.Models.DBEntities;

namespace Reminder.Services
{
    public interface IReminderService
    {
        Task<bool> CreateReminderAsync(ReminderViewModel reminder);
    }
}
