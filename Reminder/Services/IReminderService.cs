using Reminder.Models.DBEntities;

namespace Reminder.Services
{
    public interface IReminderService
    {
        /// <summary>
        /// Creates a reminder and schedules notification jobs (email, SMS, WhatsApp) for each schedule.
        /// </summary>
        Task<bool> CreateReminderAsync(ReminderViewModel reminder);
        
        /// <summary>
        /// Gets all reminders for a specific user.
        /// </summary>
        Task<IEnumerable<ReminderViewModel>> GetUserRemindersAsync(string userId);
        
        /// <summary>
        /// Gets a specific reminder by ID.
        /// </summary>
        Task<ReminderViewModel?> GetReminderByIdAsync(int reminderId);
        
        /// <summary>
        /// Updates an existing reminder.
        /// </summary>
        Task<bool> UpdateReminderAsync(ReminderViewModel reminder);
        
        /// <summary>
        /// Deletes a reminder by ID for a specific user.
        /// </summary>
        Task<bool> DeleteReminderAsync(int reminderId, string userId);
        
        /// <summary>
        /// Deletes a reminder by title or ID for a specific user.
        /// </summary>
        Task<bool> DeleteReminderAsync(string userId, string? title = null, int? id = null);
        
        /// <summary>
        /// Deletes a specific schedule and its associated Hangfire jobs.
        /// </summary>
        Task<bool> DeleteScheduleAsync(int scheduleId, string userId);
    }
}
