using Microsoft.Extensions.Configuration;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using Reminder.Models;

namespace Reminder.Services
{
    public class SmsService : ISmsService
    {
        private readonly IConfiguration _configuration;
        private readonly ILoggingService _loggingService;
        private readonly SchedulerDbContext _context;

        public SmsService(IConfiguration configuration, ILoggingService loggingService, SchedulerDbContext context)
        {
            _configuration = configuration;
            _loggingService = loggingService;
            _context = context;
            
            // Initialize Twilio
            TwilioClient.Init(
                _configuration["Twilio:AccountSid"],
                _configuration["Twilio:AuthToken"]
            );
        }

        public async Task<bool> SendVerificationCodeAsync(string phoneNumber, string code)
        {
            try
            {
                var message = $"Your Reminder App verification code is: {code}. This code will expire in 10 minutes.";
                
                var messageResource = await MessageResource.CreateAsync(
                    body: message,
                    from: new PhoneNumber(_configuration["Twilio:FromPhoneNumber"]),
                    to: new PhoneNumber(phoneNumber)
                );

                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error sending SMS verification code to: {PhoneNumber}", phoneNumber);
                return false;
            }
        }

        public async Task<bool> SendReminderSmsAsync(string phoneNumber, string reminderMessage, DateTime reminderTime, int scheduleId)
        {
            try
            {
                var formattedTime = reminderTime.ToString("MMMM dd, yyyy 'at' h:mm tt");
                var message = $"Reminder: {reminderMessage} - Scheduled for {formattedTime}. Thank you for using Reminder App!";
                
                var messageResource = await MessageResource.CreateAsync(
                    body: message,
                    from: new PhoneNumber(_configuration["Twilio:FromPhoneNumber"]),
                    to: new PhoneNumber(phoneNumber)
                );
                if (messageResource != null)
                {
                    var schedule = await _context.Schedules.FindAsync(scheduleId);
                    if (schedule != null)
                    {
                        schedule.IsReminderSent = true;
                        await _context.SaveChangesAsync();
                    }
                }
                return messageResource != null;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error sending SMS reminder to: {PhoneNumber}", phoneNumber);
                return false;
            }
        }

        public async Task<bool> SendWelcomeSmsAsync(string phoneNumber, string name)
        {
            try
            {
                var message = $"Welcome to Reminder App, {name}! Your account is now fully activated. You can start creating reminders right away.";
                
                var messageResource = await MessageResource.CreateAsync(
                    body: message,
                    from: new PhoneNumber(_configuration["Twilio:FromPhoneNumber"]),
                    to: new PhoneNumber(phoneNumber)
                );

                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error sending SMS welcome message to: {PhoneNumber}", phoneNumber);
                return false;
            }
        }

        // Optionally, implement WhatsApp notification via Twilio
        // public async Task<bool> SendWhatsAppReminderAsync(string phoneNumber, string reminderMessage, DateTime reminderTime)
        // {
        //     // Implement WhatsApp sending logic here
        //     return true;
        // }
    }
} 