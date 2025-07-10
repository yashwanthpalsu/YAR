using Reminder.Models.DBEntities;

namespace Reminder.Services
{
    public interface IAdminService
    {
        /// <summary>
        /// Ensures the Admin role exists in the database
        /// </summary>
        Task<bool> EnsureAdminRoleExistsAsync();
        
        /// <summary>
        /// Assigns admin role to a user by email
        /// </summary>
        Task<bool> AssignAdminRoleAsync(string email);
        
        /// <summary>
        /// Removes admin role from a user by email
        /// </summary>
        Task<bool> RemoveAdminRoleAsync(string email);
        
        /// <summary>
        /// Checks if a user has admin role
        /// </summary>
        Task<bool> IsUserAdminAsync(string email);
        
        /// <summary>
        /// Gets all admin users
        /// </summary>
        Task<IEnumerable<ApplicationUser>> GetAdminUsersAsync();
    }
} 