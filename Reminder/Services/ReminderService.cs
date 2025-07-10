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
                        var emailJobId = BackgroundJob.Schedule(() => _emailService.SendReminderEmailAsync(user.Email, user.FullName, reminder.Message, scheduledDateTime), scheduledDateTime - DateTime.Now);
                        schedule.EmailJobId = emailJobId;
                    }
                    if (reminder.IsTextModeSelected && !string.IsNullOrEmpty(user.PhoneNumber))
                    {
                        var smsJobId = BackgroundJob.Schedule(() => _smsService.SendReminderSmsAsync(user.PhoneNumber, reminder.Message, scheduledDateTime), scheduledDateTime - DateTime.Now);
                        schedule.SmsJobId = smsJobId;
                    }
                    // WhatsApp: Use Twilio for WhatsApp if configured (optional, example below)
                    // if (reminder.IsCallModeSelected && !string.IsNullOrEmpty(user.PhoneNumber))
                    // {
                    //     var callJobId = BackgroundJob.Schedule(() => _smsService.SendWhatsAppReminderAsync(user.PhoneNumber, reminder.Message, scheduledDateTime), scheduledDateTime - DateTime.Now);
                    //     schedule.CallJobId = callJobId;
                    // }
                }
                
                // Save changes again to store the job IDs
                await _context.SaveChangesAsync();

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

        public async Task<IEnumerable<ReminderViewModel>> GetUserRemindersAsync(string userId)
        {
            try
            {
                var reminders = await _context.Reminders
                    .Where(r => r.UserId == userId)
                    .Include(r => r.Schedules)
                    .OrderByDescending(r => r.ReminderId)
                    .ToListAsync();

                return reminders;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error retrieving reminders for user {UserId}", userId);
                return Enumerable.Empty<ReminderViewModel>();
            }
        }

        public async Task<ReminderViewModel?> GetReminderByIdAsync(int reminderId)
        {
            try
            {
                var reminder = await _context.Reminders
                    .Include(r => r.Schedules)
                    .FirstOrDefaultAsync(r => r.ReminderId == reminderId);

                return reminder;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error retrieving reminder with ID {ReminderId}", reminderId);
                return null;
            }
        }

        public async Task<bool> UpdateReminderAsync(ReminderViewModel reminder)
        {
            using var logContext = _loggingService.PushProperty("UserId", reminder.UserId);
            
            try
            {
                var existingReminder = await _context.Reminders
                    .Include(r => r.Schedules)
                    .FirstOrDefaultAsync(r => r.ReminderId == reminder.ReminderId);

                if (existingReminder == null)
                {
                    _loggingService.LogWarning("Reminder not found for update: {ReminderId}", reminder.ReminderId);
                    return false;
                }

                // Update basic properties
                existingReminder.Name = reminder.Name;
                existingReminder.Message = reminder.Message;
                existingReminder.ImportanceLevel = reminder.ImportanceLevel;
                existingReminder.IsEmailModeSelected = reminder.IsEmailModeSelected;
                existingReminder.IsTextModeSelected = reminder.IsTextModeSelected;
                existingReminder.IsCallModeSelected = reminder.IsCallModeSelected;

                // Remove existing schedules that haven't been sent yet
                var unsentSchedules = existingReminder.Schedules.Where(s => !s.IsReminderSent).ToList();
                foreach (var schedule in unsentSchedules)
                {
                    // Delete Hangfire jobs before removing the schedule
                    if (!string.IsNullOrEmpty(schedule.EmailJobId))
                    {
                        BackgroundJob.Delete(schedule.EmailJobId);
                        _loggingService.LogInformation("Deleted email job {JobId} for schedule {ScheduleId}", schedule.EmailJobId, schedule.ScheduleId);
                    }
                    if (!string.IsNullOrEmpty(schedule.SmsJobId))
                    {
                        BackgroundJob.Delete(schedule.SmsJobId);
                        _loggingService.LogInformation("Deleted SMS job {JobId} for schedule {ScheduleId}", schedule.SmsJobId, schedule.ScheduleId);
                    }
                    if (!string.IsNullOrEmpty(schedule.CallJobId))
                    {
                        BackgroundJob.Delete(schedule.CallJobId);
                        _loggingService.LogInformation("Deleted call job {JobId} for schedule {ScheduleId}", schedule.CallJobId, schedule.ScheduleId);
                    }
                    
                    _context.Schedules.Remove(schedule);
                }

                // Add new schedules
                if (reminder.Schedules != null)
                {
                    foreach (var schedule in reminder.Schedules)
                    {
                        schedule.ScheduleId = 0; // Let DB generate new ID
                        schedule.ReminderId = reminder.ReminderId;
                        if (schedule.Date.Kind != DateTimeKind.Utc)
                        {
                            schedule.Date = DateTime.SpecifyKind(schedule.Date, DateTimeKind.Utc);
                        }
                        existingReminder.Schedules.Add(schedule);
                    }
                }

                await _context.SaveChangesAsync();

                // Reschedule Hangfire jobs for new schedules
                var user = await _userManager.FindByIdAsync(reminder.UserId);
                if (user != null)
                {
                    foreach (var schedule in reminder.Schedules)
                    {
                        var scheduledDateTime = schedule.Date.Date + schedule.Time;
                        if (reminder.IsEmailModeSelected && !string.IsNullOrEmpty(user.Email))
                        {
                            var emailJobId = BackgroundJob.Schedule(() => _emailService.SendReminderEmailAsync(user.Email, user.FullName, reminder.Message, scheduledDateTime), scheduledDateTime - DateTime.Now);
                            schedule.EmailJobId = emailJobId;
                        }
                        if (reminder.IsTextModeSelected && !string.IsNullOrEmpty(user.PhoneNumber))
                        {
                            var smsJobId = BackgroundJob.Schedule(() => _smsService.SendReminderSmsAsync(user.PhoneNumber, reminder.Message, scheduledDateTime), scheduledDateTime - DateTime.Now);
                            schedule.SmsJobId = smsJobId;
                        }
                    }
                }
                
                // Save changes to store the new job IDs
                await _context.SaveChangesAsync();

                _loggingService.LogInformation("Reminder updated successfully: {ReminderId}", reminder.ReminderId);
                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error updating reminder {ReminderId}", reminder.ReminderId);
                return false;
            }
        }

        public async Task<bool> DeleteReminderAsync(int reminderId, string userId)
        {
            try
            {
                var reminder = await _context.Reminders
                    .Include(r => r.Schedules)
                    .FirstOrDefaultAsync(r => r.ReminderId == reminderId && r.UserId == userId);

                if (reminder == null)
                {
                    _loggingService.LogWarning("Reminder not found for deletion: {ReminderId} by user {UserId}", reminderId, userId);
                    return false;
                }

                // Delete Hangfire jobs for all schedules before removing them
                foreach (var schedule in reminder.Schedules)
                {
                    if (!string.IsNullOrEmpty(schedule.EmailJobId))
                    {
                        BackgroundJob.Delete(schedule.EmailJobId);
                        _loggingService.LogInformation("Deleted email job {JobId} for schedule {ScheduleId}", schedule.EmailJobId, schedule.ScheduleId);
                    }
                    if (!string.IsNullOrEmpty(schedule.SmsJobId))
                    {
                        BackgroundJob.Delete(schedule.SmsJobId);
                        _loggingService.LogInformation("Deleted SMS job {JobId} for schedule {ScheduleId}", schedule.SmsJobId, schedule.ScheduleId);
                    }
                    if (!string.IsNullOrEmpty(schedule.CallJobId))
                    {
                        BackgroundJob.Delete(schedule.CallJobId);
                        _loggingService.LogInformation("Deleted call job {JobId} for schedule {ScheduleId}", schedule.CallJobId, schedule.ScheduleId);
                    }
                }

                // Remove all schedules
                _context.Schedules.RemoveRange(reminder.Schedules);
                
                // Remove the reminder
                _context.Reminders.Remove(reminder);
                
                await _context.SaveChangesAsync();

                _loggingService.LogInformation("Reminder deleted successfully: {ReminderId} by user {UserId}", reminderId, userId);
                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error deleting reminder {ReminderId} by user {UserId}", reminderId, userId);
                return false;
            }
        }

        public async Task<bool> DeleteScheduleAsync(int scheduleId, string userId)
        {
            try
            {
                var schedule = await _context.Schedules
                    .Include(s => s.Reminder)
                    .FirstOrDefaultAsync(s => s.ScheduleId == scheduleId && s.Reminder.UserId == userId);

                if (schedule == null)
                {
                    _loggingService.LogWarning("Schedule not found for deletion: {ScheduleId} by user {UserId}", scheduleId, userId);
                    return false;
                }

                // Delete Hangfire jobs before removing the schedule
                if (!string.IsNullOrEmpty(schedule.EmailJobId))
                {
                    BackgroundJob.Delete(schedule.EmailJobId);
                    _loggingService.LogInformation("Deleted email job {JobId} for schedule {ScheduleId}", schedule.EmailJobId, schedule.ScheduleId);
                }
                if (!string.IsNullOrEmpty(schedule.SmsJobId))
                {
                    BackgroundJob.Delete(schedule.SmsJobId);
                    _loggingService.LogInformation("Deleted SMS job {JobId} for schedule {ScheduleId}", schedule.SmsJobId, schedule.ScheduleId);
                }
                if (!string.IsNullOrEmpty(schedule.CallJobId))
                {
                    BackgroundJob.Delete(schedule.CallJobId);
                    _loggingService.LogInformation("Deleted call job {JobId} for schedule {ScheduleId}", schedule.CallJobId, schedule.ScheduleId);
                }

                // Remove the schedule
                _context.Schedules.Remove(schedule);
                await _context.SaveChangesAsync();

                _loggingService.LogInformation("Schedule deleted successfully: {ScheduleId} by user {UserId}", scheduleId, userId);
                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error deleting schedule {ScheduleId} by user {UserId}", scheduleId, userId);
                return false;
            }
        }
    }
}
