using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SPMS.Models
{
    // ─── Enums ───────────────────────────────────────────────────────────────────

    public enum UserRole { Admin, Student, Tutor, Company }

    public enum ProjectStatus
    {
        PendingAdminReview,
        AdminApproved,
        AdminRejected,
        TutorReviewPending,
        TutorApproved,
        TutorRejected
    }

    public enum ProjectCategory { Internal, External }

    public enum TutorialGroupStatus
    {
        Forming,
        Ready,
        AwaitingApproval,
        Approved,
        Rejected
    }

    public enum TutorGroupRequestStatus { Pending, Approved, Rejected }

    // ─── Core Entities ────────────────────────────────────────────────────────

    public class User
    {
        public int Id { get; set; }
        [Required, StringLength(100)] public string FullName { get; set; } = "";
        [Required, StringLength(200)] public string Email { get; set; } = "";
        [Required] public string PasswordHash { get; set; } = "";
        public UserRole Role { get; set; }
        public DateTime DOB { get; set; }
        [StringLength(20)] public string? Phone { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? StudentNumber { get; set; }
        public string? Program { get; set; }
        public int? Semester { get; set; }
        public string? Department { get; set; }
        public string? Expertise { get; set; }
        public string? CompanyName { get; set; }
        public string? Industry { get; set; }
        public string? ABN { get; set; }

        public ICollection<Project> SubmittedProjects { get; set; } = new List<Project>();
        public ICollection<Project> TutoredProjects { get; set; } = new List<Project>();
        public ICollection<StudentProjectSelection> ProjectSelections { get; set; } = new List<StudentProjectSelection>();
        public ICollection<Message> SentMessages { get; set; } = new List<Message>();
        public ICollection<Message> ReceivedMessages { get; set; } = new List<Message>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }

    public class Project
    {
        public int Id { get; set; }
        [Required, StringLength(200)] public string Title { get; set; } = "";
        [Required] public string Description { get; set; } = "";

        public int CompanyId { get; set; }
        public int? TutorId { get; set; }

        public ProjectStatus Status { get; set; } = ProjectStatus.PendingAdminReview;
        public ProjectCategory? Category { get; set; }
        public string? FilePath { get; set; }
        public string? AdminNotes { get; set; }
        public string? TutorFeedback { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [ForeignKey("CompanyId")] public User? Company { get; set; }
        [ForeignKey("TutorId")] public User? Tutor { get; set; }

        public ICollection<StudentProjectSelection> StudentSelections { get; set; } = new List<StudentProjectSelection>();
    }

    public class StudentProjectSelection
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public int ProjectId { get; set; }
        public DateTime SelectedAt { get; set; } = DateTime.Now;

        [ForeignKey("StudentId")] public User? Student { get; set; }
        [ForeignKey("ProjectId")] public Project? Project { get; set; }
    }

    public class Message
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        [Required] public string Content { get; set; } = "";
        public DateTime SentAt { get; set; } = DateTime.Now;
        public bool IsRead { get; set; } = false;

        [ForeignKey("SenderId")] public User? Sender { get; set; }
        [ForeignKey("ReceiverId")] public User? Receiver { get; set; }
    }

    public class Notification
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        [Required, StringLength(200)] public string Title { get; set; } = "";
        [Required] public string Body { get; set; } = "";
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? Link { get; set; }

        [ForeignKey("UserId")] public User? User { get; set; }
    }

    public class Tutorial
    {
        public int Id { get; set; }
        [Required, StringLength(150)] public string Name { get; set; } = "";
        public int TutorId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("TutorId")] public User? Tutor { get; set; }
        public ICollection<TutorialEnrollment> Enrollments { get; set; } = new List<TutorialEnrollment>();
        public ICollection<TutorialGroup> Groups { get; set; } = new List<TutorialGroup>();
        public ICollection<TutorialAnnouncement> Announcements { get; set; } = new List<TutorialAnnouncement>();
    }

    public class TutorialEnrollment
    {
        public int Id { get; set; }
        public int TutorialId { get; set; }
        public int StudentId { get; set; }
        public DateTime EnrolledAt { get; set; } = DateTime.Now;

        [ForeignKey("TutorialId")] public Tutorial? Tutorial { get; set; }
        [ForeignKey("StudentId")] public User? Student { get; set; }
    }

    public class TutorialGroup
    {
        public int Id { get; set; }
        public int TutorialId { get; set; }
        [Required, StringLength(150)] public string Name { get; set; } = "";
        public TutorialGroupStatus Status { get; set; } = TutorialGroupStatus.Forming;
        public int CreatedByStudentId { get; set; }
        public int? ApprovedProjectId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("TutorialId")] public Tutorial? Tutorial { get; set; }
        [ForeignKey("CreatedByStudentId")] public User? CreatedByStudent { get; set; }
        [ForeignKey("ApprovedProjectId")] public Project? ApprovedProject { get; set; }
        public ICollection<TutorialGroupMember> Members { get; set; } = new List<TutorialGroupMember>();
        public ICollection<TutorialGroupRequest> GroupRequests { get; set; } = new List<TutorialGroupRequest>();
        public ICollection<TutorialGroupProjectRequest> ProjectRequests { get; set; } = new List<TutorialGroupProjectRequest>();
    }

    public class TutorialGroupMember
    {
        public int Id { get; set; }
        public int TutorialGroupId { get; set; }
        public int StudentId { get; set; }
        public bool IsLeader { get; set; } = false;
        public DateTime JoinedAt { get; set; } = DateTime.Now;

        [ForeignKey("TutorialGroupId")] public TutorialGroup? TutorialGroup { get; set; }
        [ForeignKey("StudentId")] public User? Student { get; set; }
    }

    public class TutorialGroupRequest
    {
        public int Id { get; set; }
        public int TutorialGroupId { get; set; }
        public TutorGroupRequestStatus Status { get; set; } = TutorGroupRequestStatus.Pending;
        public string? Message { get; set; }
        public string? TutorFeedback { get; set; }
        public DateTime RequestedAt { get; set; } = DateTime.Now;
        public DateTime? RespondedAt { get; set; }

        [ForeignKey("TutorialGroupId")] public TutorialGroup? TutorialGroup { get; set; }
    }

    public class TutorialGroupProjectRequest
    {
        public int Id { get; set; }
        public int TutorialGroupId { get; set; }
        public int ProjectId { get; set; }
        public TutorGroupRequestStatus Status { get; set; } = TutorGroupRequestStatus.Pending;
        public string? Message { get; set; }
        public string? TutorFeedback { get; set; }
        public DateTime RequestedAt { get; set; } = DateTime.Now;
        public DateTime? RespondedAt { get; set; }

        [ForeignKey("TutorialGroupId")] public TutorialGroup? TutorialGroup { get; set; }
        [ForeignKey("ProjectId")] public Project? Project { get; set; }
    }

    public class TutorialAnnouncement
    {
        public int Id { get; set; }
        public int TutorialId { get; set; }
        [Required, StringLength(200)] public string Title { get; set; } = "";
        [Required] public string Body { get; set; } = "";
        public DateTime PostedAt { get; set; } = DateTime.Now;

        [ForeignKey("TutorialId")] public Tutorial? Tutorial { get; set; }
    }

    public class ProjectChecklist
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        [Required, StringLength(50)] public string ItemKey { get; set; } = "";
        public string? FilePath { get; set; }
        [StringLength(255)] public string? OriginalFileName { get; set; }
        public DateTime? UploadedAt { get; set; }
        public int? UploadedByStudentId { get; set; }

        [ForeignKey("ProjectId")] public Project? Project { get; set; }
        [ForeignKey("UploadedByStudentId")] public User? UploadedByStudent { get; set; }
    }

    public class TutorialWeeklyProgress
    {
        public int Id { get; set; }
        public int TutorialGroupId { get; set; }
        public int ProjectId { get; set; }
        public int WeekNumber { get; set; }
        [Required] public string StudentUpdate { get; set; } = "";
        public int SubmittedByStudentId { get; set; }
        public string? FilePath { get; set; }
        [StringLength(255)] public string? OriginalFileName { get; set; }
        public string? TutorFeedback { get; set; }
        public int? TutorId { get; set; }
        public DateTime StudentSubmittedAt { get; set; } = DateTime.Now;
        public DateTime? TutorRespondedAt { get; set; }

        [ForeignKey("TutorialGroupId")] public TutorialGroup? TutorialGroup { get; set; }
        [ForeignKey("ProjectId")] public Project? Project { get; set; }
        [ForeignKey("SubmittedByStudentId")] public User? SubmittedByStudent { get; set; }
        [ForeignKey("TutorId")] public User? Tutor { get; set; }
    }

    // ─── View Models ──────────────────────────────────────────────────────────

    public class LoginVM
    {
        [Required, EmailAddress] public string Email { get; set; } = "";
        [Required] public string Password { get; set; } = "";
    }

    public class CreateUserVM
    {
        [Required, StringLength(100)] public string FullName { get; set; } = "";
        [Required, EmailAddress] public string Email { get; set; } = "";
        [Required] public UserRole Role { get; set; }
        [Required, DataType(DataType.Date)] public DateTime DOB { get; set; } = new DateTime(2000, 1, 1);
        [StringLength(20)] public string? Phone { get; set; }
        public string? StudentNumber { get; set; }
        public string? Program { get; set; }
        public int? Semester { get; set; }
        public string? Department { get; set; }
        public string? Expertise { get; set; }
        public string? CompanyName { get; set; }
        public string? Industry { get; set; }
        public string? ABN { get; set; }
    }

    public class EditUserVM
    {
        public int Id { get; set; }
        [Required, StringLength(100)] public string FullName { get; set; } = "";
        [Required, EmailAddress] public string Email { get; set; } = "";
        public UserRole Role { get; set; }
        [Required, DataType(DataType.Date)] public DateTime DOB { get; set; }
        [StringLength(20)] public string? Phone { get; set; }
        public bool IsActive { get; set; }
        public string? StudentNumber { get; set; }
        public string? Program { get; set; }
        public int? Semester { get; set; }
        public string? Department { get; set; }
        public string? Expertise { get; set; }
        public string? CompanyName { get; set; }
        public string? Industry { get; set; }
        public string? ABN { get; set; }
    }

    public class SubmitProjectVM
    {
        [Required, StringLength(200)] public string Title { get; set; } = "";
        [Required] public string Description { get; set; } = "";
        public ProjectCategory? Category { get; set; }
        public IFormFile? File { get; set; }
    }

    public class EditProjectVM
    {
        public int Id { get; set; }
        [Required, StringLength(200)] public string Title { get; set; } = "";
        [Required] public string Description { get; set; } = "";
        public ProjectCategory? Category { get; set; }
        public string? AdminNotes { get; set; }
    }

    public class SubmitTutorialWeeklyProgressVM
    {
        public int TutorialGroupId { get; set; }
        public int ProjectId { get; set; }
        [Required] public string StudentUpdate { get; set; } = "";
        public IFormFile? File { get; set; }
    }

    public class TutorialWeeklyFeedbackVM
    {
        public int EntryId { get; set; }
        [Required] public string TutorFeedback { get; set; } = "";
    }

    public class SendMessageVM
    {
        [Required] public int ReceiverId { get; set; }
        [Required] public string Content { get; set; } = "";
    }
}
