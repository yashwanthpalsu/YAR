using Microsoft.EntityFrameworkCore;
using Reminder.Models;
using Reminder.Models.DBEntities;
using Hangfire;
using Microsoft.AspNetCore.Identity;

namespace Reminder.Services
{
    public class ReminderService : IReminderService
    {
        private readonly SchedulerDbContext _context;
        private readonly ILoggingService _loggingService;
        private readonly IEmailService _emailService;
        private readonly ISmsService _smsService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReminderService(SchedulerDbContext context, ILoggingService loggingService, IEmailService emailService, ISmsService smsService, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _loggingService = loggingService;
            _emailService = emailService;
            _smsService = smsService;
            _userManager = userManager;
        }

        public async Task<bool> CreateReminderAsync(ReminderViewModel reminder)
        {
            using var logContext = _loggingService.PushProperty("UserId", reminder.UserId);
            
            try
            {
                _loggingService.LogInformation("Entered Create Reminder");
                // Ensure all schedule dates are UTC and ScheduleId is 0 before saving
                if (reminder.Schedules != null && reminder.Schedules.Any())
                {
                    foreach (var schedule in reminder.Schedules)
                    {
                        schedule.ScheduleId = 0; // Let DB generate PK
                        if (schedule.Date.Kind != DateTimeKind.Utc)
                        {
                            schedule.Date = DateTime.SpecifyKind(schedule.Date, DateTimeKind.Utc);
                        }
                    }
                }
                // Add the reminder (with schedules) to the context
                _context.Reminders.Add(reminder);
                // Save changes to get the generated ReminderId and save schedules
                await _context.SaveChangesAsync();

                if (reminder.Schedules == null || !reminder.Schedules.Any())
                {
                    _loggingService.LogWarning("No schedules provided for reminder {ReminderId}", reminder.ReminderId);
                }

                // Fetch user info for notifications
                var user = await _userManager.FindByIdAsync(reminder.UserId);
                if (user == null)
                {
                    _loggingService.LogError("User not found for reminder notification scheduling: {UserId}", reminder.UserId);
                    return false;
                }

                // Schedule Hangfire jobs for each schedule
                foreach (var schedule in reminder.Schedules)
                {
                    var scheduledDateTime = schedule.Date.Date + schedule.Time;
                    if (reminder.IsEmailModeSelected && !string.IsNullOrEmpty(user.Email))
                    {
                        BackgroundJob.Schedule(() => _emailService.SendReminderEmailAsync(user.Email, user.FullName, reminder.Message, scheduledDateTime), scheduledDateTime - DateTime.Now);
                    }
                    if (reminder.IsTextModeSelected && !string.IsNullOrEmpty(user.PhoneNumber))
                    {
                        BackgroundJob.Schedule(() => _smsService.SendReminderSmsAsync(user.PhoneNumber, reminder.Message, scheduledDateTime), scheduledDateTime - DateTime.Now);
                    }
                    // WhatsApp: Use Twilio for WhatsApp if configured (optional, example below)
                    // if (reminder.IsCallModeSelected && !string.IsNullOrEmpty(user.PhoneNumber))
                    // {
                    //     BackgroundJob.Schedule(() => _smsService.SendWhatsAppReminderAsync(user.PhoneNumber, reminder.Message, scheduledDateTime), scheduledDateTime - DateTime.Now);
                    // }
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
