using Microsoft.EntityFrameworkCore;
using SPMS.Models;

namespace SPMS.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<StudentProjectSelection> StudentProjectSelections { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<ProjectChecklist> ProjectChecklists { get; set; }
        public DbSet<Tutorial> Tutorials { get; set; }
        public DbSet<TutorialEnrollment> TutorialEnrollments { get; set; }
        public DbSet<TutorialGroup> TutorialGroups { get; set; }
        public DbSet<TutorialGroupMember> TutorialGroupMembers { get; set; }
        public DbSet<TutorialGroupRequest> TutorialGroupRequests { get; set; }
        public DbSet<TutorialGroupProjectRequest> TutorialGroupProjectRequests { get; set; }
        public DbSet<TutorialAnnouncement> TutorialAnnouncements { get; set; }
        public DbSet<TutorialWeeklyProgress> TutorialWeeklyProgressEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);

            mb.Entity<Project>()
                .HasOne(p => p.Company)
                .WithMany(u => u.SubmittedProjects)
                .HasForeignKey(p => p.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            mb.Entity<Project>()
                .HasOne(p => p.Tutor)
                .WithMany(u => u.TutoredProjects)
                .HasForeignKey(p => p.TutorId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            mb.Entity<StudentProjectSelection>()
                .HasIndex(s => new { s.StudentId, s.ProjectId })
                .IsUnique();

            mb.Entity<StudentProjectSelection>()
                .HasOne(s => s.Student)
                .WithMany(u => u.ProjectSelections)
                .HasForeignKey(s => s.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<StudentProjectSelection>()
                .HasOne(s => s.Project)
                .WithMany(p => p.StudentSelections)
                .HasForeignKey(s => s.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany(u => u.SentMessages)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            mb.Entity<Message>()
                .HasOne(m => m.Receiver)
                .WithMany(u => u.ReceivedMessages)
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            mb.Entity<Tutorial>()
                .HasOne(t => t.Tutor)
                .WithMany()
                .HasForeignKey(t => t.TutorId)
                .OnDelete(DeleteBehavior.Restrict);

            mb.Entity<TutorialEnrollment>()
                .HasOne(te => te.Tutorial)
                .WithMany(t => t.Enrollments)
                .HasForeignKey(te => te.TutorialId)
                .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<TutorialEnrollment>()
                .HasOne(te => te.Student)
                .WithMany()
                .HasForeignKey(te => te.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            mb.Entity<TutorialEnrollment>()
                .HasIndex(te => te.StudentId)
                .IsUnique();

            mb.Entity<TutorialGroup>()
                .HasOne(tg => tg.Tutorial)
                .WithMany(t => t.Groups)
                .HasForeignKey(tg => tg.TutorialId)
                .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<TutorialGroup>()
                .HasOne(tg => tg.CreatedByStudent)
                .WithMany()
                .HasForeignKey(tg => tg.CreatedByStudentId)
                .OnDelete(DeleteBehavior.Restrict);

            mb.Entity<TutorialGroup>()
                .HasOne(tg => tg.ApprovedProject)
                .WithMany()
                .HasForeignKey(tg => tg.ApprovedProjectId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            mb.Entity<TutorialGroupMember>()
                .HasOne(m => m.TutorialGroup)
                .WithMany(tg => tg.Members)
                .HasForeignKey(m => m.TutorialGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<TutorialGroupMember>()
                .HasOne(m => m.Student)
                .WithMany()
                .HasForeignKey(m => m.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            mb.Entity<TutorialGroupMember>()
                .HasIndex(m => new { m.TutorialGroupId, m.StudentId })
                .IsUnique();

            mb.Entity<TutorialGroupRequest>()
                .HasOne(r => r.TutorialGroup)
                .WithMany(tg => tg.GroupRequests)
                .HasForeignKey(r => r.TutorialGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<TutorialGroupProjectRequest>()
                .HasOne(r => r.TutorialGroup)
                .WithMany(tg => tg.ProjectRequests)
                .HasForeignKey(r => r.TutorialGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<TutorialGroupProjectRequest>()
                .HasOne(r => r.Project)
                .WithMany()
                .HasForeignKey(r => r.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            mb.Entity<TutorialAnnouncement>()
                .HasOne(a => a.Tutorial)
                .WithMany(t => t.Announcements)
                .HasForeignKey(a => a.TutorialId)
                .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<ProjectChecklist>()
                .HasOne(c => c.Project)
                .WithMany()
                .HasForeignKey(c => c.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<TutorialWeeklyProgress>()
                .HasOne(wp => wp.TutorialGroup)
                .WithMany()
                .HasForeignKey(wp => wp.TutorialGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<TutorialWeeklyProgress>()
                .HasOne(wp => wp.Project)
                .WithMany()
                .HasForeignKey(wp => wp.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            mb.Entity<TutorialWeeklyProgress>()
                .HasOne(wp => wp.SubmittedByStudent)
                .WithMany()
                .HasForeignKey(wp => wp.SubmittedByStudentId)
                .OnDelete(DeleteBehavior.Restrict);

            mb.Entity<TutorialWeeklyProgress>()
                .HasOne(wp => wp.Tutor)
                .WithMany()
                .HasForeignKey(wp => wp.TutorId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
