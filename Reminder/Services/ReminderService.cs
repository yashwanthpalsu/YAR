using Microsoft.EntityFrameworkCore;
using Reminder.Models;
using Reminder.Models.DBEntities;

namespace Reminder.Services
{
    public class ReminderService : IReminderService
    {
        private readonly SchedulerDbContext _context;
        private readonly ILoggingService _loggingService;

        public ReminderService(SchedulerDbContext context, ILoggingService loggingService)
        {
            _context = context;
            _loggingService = loggingService;
        }

        public async Task<bool> CreateReminderAsync(ReminderViewModel reminder)
        {
            using var logContext = _loggingService.PushProperty("UserId", reminder.UserId);
            
            try
            {
                _loggingService.LogInformation("Entered Create Reminder");
                // Add the reminder to the context
                _context.Reminders.Add(reminder);
                
                // Save changes to get the generated ReminderId
                await _context.SaveChangesAsync();

                // If there are schedules, add them with the generated ReminderId
                if (reminder.Schedules != null && reminder.Schedules.Any())
                {

                    foreach (var schedule in reminder.Schedules)
                    {
                        schedule.ReminderId = reminder.ReminderId;
                        _context.Schedules.Add(schedule);
                        
                    }
                    
                    // Save the schedules
                    await _context.SaveChangesAsync();
                }
                else
                {
                    _loggingService.LogWarning("No schedules provided for reminder {ReminderId}", reminder.ReminderId);
                }

                return true;
            }
            catch (DbUpdateException dbEx)
            {
                _loggingService.LogError(dbEx, "Database error occurred while creating reminder for user {UserId}. " +
                    "Error: {ErrorMessage}", reminder.UserId, dbEx.InnerException?.Message ?? dbEx.Message);
                return false;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Unexpected error occurred while creating reminder for user {UserId}. " +
                    "Error: {ErrorMessage}", reminder.UserId, ex.Message);
                return false;
            }
        }
    }
}
