using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Reminder.Models.DBEntities;

namespace Reminder.Models
{
    public class SchedulerDbContext : IdentityDbContext<ApplicationUser>
    {
        public SchedulerDbContext(DbContextOptions<SchedulerDbContext> options)
            : base(options)
        {
        }
        
        public DbSet<DBEntities.ReminderViewModel> Reminders { get; set; }
        public DbSet<DBEntities.ScheduleViewModel> Schedules { get; set; }
        public DbSet<DBEntities.ChatMessage> ChatMessages { get; set; }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Reminder entity
            modelBuilder.Entity<DBEntities.ReminderViewModel>(entity =>
            {
                entity.HasKey(e => e.ReminderId);
                entity.Property(e => e.Message).HasColumnType("varchar(250)");
                entity.Property(e => e.Name).HasColumnType("varchar(250)");
                
                // Configure many-to-one relationship with ApplicationUser
                entity.HasOne<ApplicationUser>()
                      .WithMany(u => u.Reminders)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade)
                      .IsRequired();
                
                // Configure one-to-many relationship with Schedule
                entity.HasMany(e => e.Schedules)
                      .WithOne(e => e.Reminder)
                      .HasForeignKey(e => e.ReminderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure Schedule entity
            modelBuilder.Entity<DBEntities.ScheduleViewModel>(entity =>
            {
                entity.HasKey(e => e.ScheduleId);
                
                // Configure many-to-one relationship with Reminder
                entity.HasOne(e => e.Reminder)
                      .WithMany(e => e.Schedules)
                      .HasForeignKey(e => e.ReminderId)
                      .OnDelete(DeleteBehavior.Cascade)
                      .IsRequired();
            });

            // Configure ApplicationUser entity
            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(e => e.FirstName).HasColumnType("varchar(100)");
                entity.Property(e => e.LastName).HasColumnType("varchar(100)");
                entity.Property(e => e.Address).HasColumnType("varchar(500)");
                
                // Configure one-to-many relationship with Reminders
                entity.HasMany(e => e.Reminders)
                      .WithOne()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
