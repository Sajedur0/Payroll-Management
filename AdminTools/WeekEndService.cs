using PayrollManagement.Data;

namespace PayrollManagement.AdminTools
{
    public static class WeekEndService
    {
        public static readonly string[] AllDays =
            { "Saturday", "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" };

        public static List<string> List() =>
            DbHelper.FetchRows("SELECT DayName FROM WeekEnd ORDER BY DayName")
                .Select(r => r[0]!.ToString()!).ToList();

        public static void Add(string dayName) =>
            DbHelper.ExecuteNonQuery(
                "IF NOT EXISTS (SELECT 1 FROM WeekEnd WHERE DayName = @p0) INSERT INTO WeekEnd (DayName) VALUES (@p1)",
                ("@p0", dayName), ("@p1", dayName));

        public static void Delete(string dayName) =>
            DbHelper.ExecuteNonQuery("DELETE FROM WeekEnd WHERE DayName = @p0", ("@p0", dayName));
    }
}
