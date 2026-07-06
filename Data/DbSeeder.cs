using SPMS.Models;
using SPMS.Helpers;

namespace SPMS.Data
{
    public static class DbSeeder
    {
        public static void Seed(AppDbContext db)
        {
            if (db.Users.Any()) return;

            var adminDob = new DateTime(1985, 6, 15);
            db.Users.Add(new User
            {
                FullName = "Admin User", Email = "admin@koi.edu.au",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(PasswordHelper.GenerateFromDOB(adminDob)),
                Role = UserRole.Admin, DOB = adminDob, Phone = "(02) 9283 3583", IsActive = true
            });

            var tutorDob = new DateTime(1980, 3, 10);
            db.Users.Add(new User
            {
                FullName = "Dr. James Smith", Email = "j.smith@koi.edu.au",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(PasswordHelper.GenerateFromDOB(tutorDob)),
                Role = UserRole.Tutor, DOB = tutorDob,
                Department = "Information Technology", Expertise = "Software Engineering", IsActive = true
            });

            var s1Dob = new DateTime(2001, 9, 22);
            db.Users.Add(new User
            {
                FullName = "Alice Johnson", Email = "alice.j@student.koi.edu.au",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(PasswordHelper.GenerateFromDOB(s1Dob)),
                Role = UserRole.Student, DOB = s1Dob,
                StudentNumber = "KOI2024001", Program = "Bachelor of IT", Semester = 3, IsActive = true
            });

            var s2Dob = new DateTime(2002, 4, 11);
            db.Users.Add(new User
            {
                FullName = "Bob Chen", Email = "bob.c@student.koi.edu.au",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(PasswordHelper.GenerateFromDOB(s2Dob)),
                Role = UserRole.Student, DOB = s2Dob,
                StudentNumber = "KOI2024002", Program = "Bachelor of IT", Semester = 3, IsActive = true
            });

            var s3Dob = new DateTime(2001, 12, 5);
            db.Users.Add(new User
            {
                FullName = "Sara Williams", Email = "sara.w@student.koi.edu.au",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(PasswordHelper.GenerateFromDOB(s3Dob)),
                Role = UserRole.Student, DOB = s3Dob,
                StudentNumber = "KOI2024003", Program = "Bachelor of IT", Semester = 3, IsActive = true
            });

            var companyDob = new DateTime(1990, 12, 5);
            db.Users.Add(new User
            {
                FullName = "Tech Corp Contact", Email = "contact@techcorp.com.au",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(PasswordHelper.GenerateFromDOB(companyDob)),
                Role = UserRole.Company, DOB = companyDob,
                CompanyName = "Tech Corp Australia", Industry = "Information Technology",
                ABN = "12 345 678 901", IsActive = true
            });

            db.SaveChanges();
        }
    }
}
