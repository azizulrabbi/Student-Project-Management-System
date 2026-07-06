using Microsoft.EntityFrameworkCore;

namespace SPMS.Data
{
    public static class SchemaMigrator
    {
        public static void Apply(AppDbContext db, ILogger logger)
        {
            DropLegacyTables(db, logger);
            DropLegacyTables(db, logger);
            EnforceOneTutorialPerStudent(db, logger);
            AddChecklistFileColumns(db, logger);
            CreateTutorialWeeklyProgressTable(db, logger);
            AddWeeklyProgressFileColumns(db, logger);
        }

        private static void DropLegacyTables(AppDbContext db, ILogger logger)
        {
            // Drop in FK-safe order: dependents before parents
            var tables = new[] { "GroupMessages", "WeeklyProgressEntries", "TutorRequests", "GroupMembers", "Groups" };
            foreach (var table in tables)
            {
                var exists = db.Database
                    .SqlQueryRaw<int>(
                        @"SELECT COUNT(*) AS `Value` FROM information_schema.TABLES
                          WHERE table_schema = DATABASE() AND table_name = {0}", table)
                    .AsEnumerable()
                    .First() > 0;
                if (!exists) continue;
                db.Database.ExecuteSqlRaw($"SET FOREIGN_KEY_CHECKS=0; DROP TABLE IF EXISTS `{table}`; SET FOREIGN_KEY_CHECKS=1;");
                logger.LogInformation("Dropped legacy table {Table}.", table);
            }
        }

        private static void CreateTutorialWeeklyProgressTable(AppDbContext db, ILogger logger)
        {
            var exists = db.Database
                .SqlQueryRaw<int>(
                    @"SELECT COUNT(*) AS `Value` FROM information_schema.TABLES
                      WHERE table_schema = DATABASE() AND table_name = 'TutorialWeeklyProgressEntries'")
                .AsEnumerable()
                .First() > 0;
            if (exists) return;

            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE `TutorialWeeklyProgressEntries` (
                    `Id` INT NOT NULL AUTO_INCREMENT,
                    `TutorialGroupId` INT NOT NULL,
                    `ProjectId` INT NOT NULL,
                    `WeekNumber` INT NOT NULL,
                    `StudentUpdate` LONGTEXT NOT NULL,
                    `SubmittedByStudentId` INT NOT NULL,
                    `TutorFeedback` LONGTEXT NULL,
                    `TutorId` INT NULL,
                    `StudentSubmittedAt` DATETIME(6) NOT NULL,
                    `TutorRespondedAt` DATETIME(6) NULL,
                    CONSTRAINT `PK_TutorialWeeklyProgressEntries` PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_TWPE_TutorialGroups` FOREIGN KEY (`TutorialGroupId`) REFERENCES `TutorialGroups`(`Id`) ON DELETE CASCADE,
                    CONSTRAINT `FK_TWPE_Projects` FOREIGN KEY (`ProjectId`) REFERENCES `Projects`(`Id`) ON DELETE RESTRICT,
                    CONSTRAINT `FK_TWPE_Students` FOREIGN KEY (`SubmittedByStudentId`) REFERENCES `Users`(`Id`) ON DELETE RESTRICT,
                    CONSTRAINT `FK_TWPE_Tutor` FOREIGN KEY (`TutorId`) REFERENCES `Users`(`Id`) ON DELETE SET NULL
                ) CHARACTER SET=utf8mb4");
            logger.LogInformation("Created table TutorialWeeklyProgressEntries.");
        }

        private static void AddWeeklyProgressFileColumns(AppDbContext db, ILogger logger)
        {
            const string table = "TutorialWeeklyProgressEntries";
            AddColumnIfMissing(db, logger, table, "FilePath", "VARCHAR(500) NULL");
            AddColumnIfMissing(db, logger, table, "OriginalFileName", "VARCHAR(255) NULL");
        }

        private static void AddChecklistFileColumns(AppDbContext db, ILogger logger)
        {
            const string table = "ProjectChecklists";
            AddColumnIfMissing(db, logger, table, "FilePath", "VARCHAR(500) NULL");
            AddColumnIfMissing(db, logger, table, "OriginalFileName", "VARCHAR(255) NULL");
            AddColumnIfMissing(db, logger, table, "UploadedAt", "DATETIME(6) NULL");
            AddColumnIfMissing(db, logger, table, "UploadedByStudentId", "INT NULL");
        }

        private static void AddColumnIfMissing(AppDbContext db, ILogger logger, string table, string column, string definition)
        {
            var exists = db.Database
                .SqlQueryRaw<int>(
                    @"SELECT COUNT(*) AS `Value` FROM information_schema.COLUMNS
                      WHERE table_schema = DATABASE()
                        AND table_name = {0}
                        AND column_name = {1}",
                    table, column)
                .AsEnumerable()
                .First() > 0;
            if (exists) return;
            db.Database.ExecuteSqlRaw($"ALTER TABLE `{table}` ADD COLUMN `{column}` {definition}");
            logger.LogInformation("Added column {Column} to {Table}.", column, table);
        }

        private static void EnforceOneTutorialPerStudent(AppDbContext db, ILogger logger)
        {
            const string oldIndex = "IX_TutorialEnrollments_TutorialId_StudentId";
            const string newIndex = "IX_TutorialEnrollments_StudentId";
            const string table = "TutorialEnrollments";

            // 1. Ensure the strict unique index on StudentId exists first.
            if (!IndexExists(db, table, newIndex))
            {
                var duplicates = db.TutorialEnrollments
                    .GroupBy(e => e.StudentId)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicates.Count > 0)
                {
                    logger.LogWarning(
                        "Skipping unique index on {Table}.StudentId — {Count} student(s) enrolled in multiple tutorials (StudentIds: {Ids}). Unenroll duplicates and restart.",
                        table, duplicates.Count, string.Join(", ", duplicates));
                    return;
                }

                db.Database.ExecuteSqlRaw($"CREATE UNIQUE INDEX `{newIndex}` ON `{table}` (`StudentId`)");
                logger.LogInformation("Created unique index {Index}.", newIndex);
            }

            // 2. Try to drop the legacy composite index. If MySQL refuses (because a foreign key
            //    still references it), leave it in place — the strict StudentId index is what enforces our rule.
            if (IndexExists(db, table, oldIndex))
            {
                try
                {
                    db.Database.ExecuteSqlRaw($"DROP INDEX `{oldIndex}` ON `{table}`");
                    logger.LogInformation("Dropped legacy index {Index}.", oldIndex);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        "Could not drop legacy index {Index} (likely a foreign key still depends on it). Leaving it in place — the unique index on StudentId is still enforced. Error: {Err}",
                        oldIndex, ex.Message);
                }
            }
        }

        private static bool IndexExists(AppDbContext db, string table, string indexName)
        {
            return db.Database
                .SqlQueryRaw<int>(
                    @"SELECT COUNT(*) AS `Value` FROM information_schema.STATISTICS
                      WHERE table_schema = DATABASE()
                        AND table_name = {0}
                        AND index_name = {1}",
                    table, indexName)
                .AsEnumerable()
                .First() > 0;
        }
    }
}
