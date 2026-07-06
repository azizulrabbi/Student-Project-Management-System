namespace SPMS.Helpers
{
    public static class PasswordHelper
    {
        /// <summary>
        /// Generates password from DOB using template KOI_DayMonthYear
        /// Example: DOB = 22 Sep 2001 → KOI_22092001
        /// </summary>
        public static string GenerateFromDOB(DateTime dob)
        {
            return $"KOI_{dob.Day:D2}{dob.Month:D2}{dob.Year}";
        }
    }
}
