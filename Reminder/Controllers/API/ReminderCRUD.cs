using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Reminder.Models;
using Reminder.Models.DBEntities;
using Reminder.Services;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Reminder.Controllers.API
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReminderCRUD : ControllerBase
    {
        private readonly SchedulerDbContext _dbcontext;
        private readonly IReminderService _reminderService;
        private readonly ILoggingService _loggingService;

        public ReminderCRUD(SchedulerDbContext context, IReminderService reminderService, ILoggingService loggingService)
        {
            _dbcontext = context;
            _reminderService = reminderService;
            _loggingService = loggingService;
        }

        // GET: api/<ReminderCRUD>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ReminderViewModel>>> Get()
        {
            try
            {
                var reminders = await _dbcontext.Reminders
                    .Include(r => r.Schedules)
                    .ToListAsync();
                return Ok(reminders);
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error retrieving reminders");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET api/<ReminderCRUD>/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ReminderViewModel>> Get(int id)
        {
            try
            {
                var reminder = await _dbcontext.Reminders
                    .Include(r => r.Schedules)
                    .FirstOrDefaultAsync(r => r.ReminderId == id);

                if (reminder == null)
                {
                    return NotFound($"Reminder with ID {id} not found");
                }

                return Ok(reminder);
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error retrieving reminder with ID {ReminderId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST api/<ReminderCRUD>
        [HttpPost]
        public async Task<ActionResult<ReminderViewModel>> Post([FromBody] CreateReminderRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    _loggingService.LogWarning("Invalid model state for reminder creation: {ValidationErrors}", 
                        string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                    return BadRequest(ModelState);
                }

                // Map the request to ReminderViewModel
                var reminder = new ReminderViewModel
                {
                    UserId = "dummy",//request.UserId,
                    Name = request.Name,
                    Message = request.Message,
                    IsEmailModeSelected = request.IsEmailModeSelected,
                    IsTextModeSelected = request.IsTextModeSelected,
                    IsCallModeSelected = request.IsCallModeSelected,
                    ImportanceLevel = request.ImportanceLevel,
                    Schedules = request.Schedules.Select(s => new ScheduleViewModel
                    {
                        Date = s.Date,
                        Time = s.Time,
                        IsReminderSent = false,
                        IsAcknowledged = false
                    }).ToList()
                };

                var success = await _reminderService.CreateReminderAsync(reminder);

                if (success)
                {
                    _loggingService.LogInformation("Reminder created successfully with ID {ReminderId}", reminder.ReminderId);
                    return CreatedAtAction(nameof(Get), new { id = reminder.ReminderId }, reminder);
                }
                else
                {
                    _loggingService.LogError("Failed to create reminder for user {UserId}", reminder.UserId);
                    return StatusCode(500, "Failed to create reminder");
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Unexpected error creating reminder for user {UserId}", request.UserId);
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT api/<ReminderCRUD>/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] ReminderViewModel reminder)
        {
            try
            {
                if (id != reminder.ReminderId)
                {
                    return BadRequest("ID mismatch");
                }

                var existingReminder = await _dbcontext.Reminders
                    .Include(r => r.Schedules)
                    .FirstOrDefaultAsync(r => r.ReminderId == id);

                if (existingReminder == null)
                {
                    return NotFound($"Reminder with ID {id} not found");
                }

                // Update reminder properties
                existingReminder.Name = reminder.Name;
                existingReminder.Message = reminder.Message;
                existingReminder.IsEmailModeSelected = reminder.IsEmailModeSelected;
                existingReminder.IsTextModeSelected = reminder.IsTextModeSelected;
                existingReminder.IsCallModeSelected = reminder.IsCallModeSelected;
                existingReminder.ImportanceLevel = reminder.ImportanceLevel;

                await _dbcontext.SaveChangesAsync();
                _loggingService.LogInformation("Reminder {ReminderId} updated successfully", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error updating reminder {ReminderId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE api/<ReminderCRUD>/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var reminder = await _dbcontext.Reminders
                    .Include(r => r.Schedules)
                    .FirstOrDefaultAsync(r => r.ReminderId == id);

                if (reminder == null)
                {
                    return NotFound($"Reminder with ID {id} not found");
                }

                _dbcontext.Reminders.Remove(reminder);
                await _dbcontext.SaveChangesAsync();

                _loggingService.LogInformation("Reminder {ReminderId} deleted successfully", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error deleting reminder {ReminderId}", id);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
