using PayrollManagement.Data;

namespace PayrollManagement.AdminTools
{
    public static class WeekEndService
    {
        public static readonly string[] AllDays =
            { "Saturday", "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" };

        public static void EnsureTable()
        {
            try
            {
                DbHelper.ExecuteNonQuery(@"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='WeekEnd')
                    BEGIN
                        CREATE TABLE dbo.WeekEnd (
                            DayName NVARCHAR(20) PRIMARY KEY
                        );
                        INSERT INTO dbo.WeekEnd (DayName) VALUES ('Friday');
                    END;");
            }
            catch { }
        }

        public static List<string> List()
        {
            EnsureTable();
            try
            {
                return DbHelper.FetchRows("SELECT DayName FROM dbo.WeekEnd ORDER BY DayName")
                    .Select(r => r[0]!.ToString()!).ToList();
            }
            catch { return new List<string> { "Friday" }; }
        }

        public static void Add(string dayName)
        {
            EnsureTable();
            DbHelper.ExecuteNonQuery(
                "IF NOT EXISTS (SELECT 1 FROM dbo.WeekEnd WHERE DayName = @p0) INSERT INTO dbo.WeekEnd (DayName) VALUES (@p1)",
                ("@p0", dayName), ("@p1", dayName));
        }

        public static void Delete(string dayName)
        {
            EnsureTable();
            DbHelper.ExecuteNonQuery("DELETE FROM dbo.WeekEnd WHERE DayName = @p0", ("@p0", dayName));
        }
    }
}
