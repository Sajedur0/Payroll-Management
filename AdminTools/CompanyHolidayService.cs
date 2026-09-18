using PayrollManagement.Data;

namespace PayrollManagement.AdminTools
{
    public record CompanyHolidayRow(int HolidayId, string FestivalName, string FromDate, string ToDate, string Notes);

    public class CompanyHolidayInput
    {
        public int? HolidayId { get; set; }
        public string FestivalName { get; set; } = "";
        public string FromDate { get; set; } = "";
        public string ToDate { get; set; } = "";
        public string Notes { get; set; } = "";
    }

    public static class CompanyHolidayService
    {
        public static void EnsureTable()
        {
            try
            {
                DbHelper.ExecuteNonQuery(@"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='CompanyHoliday')
                    CREATE TABLE dbo.CompanyHoliday (
                        HolidayID INT IDENTITY(1,1) PRIMARY KEY,
                        FestivalName NVARCHAR(200) NOT NULL,
                        FromDate NVARCHAR(20) NOT NULL,
                        ToDate NVARCHAR(20) NOT NULL,
                        Notes NVARCHAR(500) NULL
                    );");
            }
            catch { }
        }

        public static List<CompanyHolidayRow> List()
        {
            EnsureTable();
            try
            {
                return DbHelper.FetchRows(
                    "SELECT HolidayID, FestivalName, FromDate, ToDate, Notes FROM dbo.CompanyHoliday ORDER BY FromDate")
                .Select(r => new CompanyHolidayRow(
                    Convert.ToInt32(r[0]), r[1]?.ToString() ?? "", r[2]?.ToString() ?? "",
                    r[3]?.ToString() ?? "", r[4]?.ToString() ?? "")).ToList();
            }
            catch { return new List<CompanyHolidayRow>(); }
        }

        public static (bool ok, string? error) Save(CompanyHolidayInput input)
        {
            EnsureTable();
            if (string.IsNullOrWhiteSpace(input.FestivalName))
                return (false, "Festival name is required");
            if (string.CompareOrdinal(input.FromDate, input.ToDate) > 0)
                return (false, "From date cannot be after To date");

            if (input.HolidayId.HasValue)
            {
                DbHelper.ExecuteNonQuery(
                    "UPDATE dbo.CompanyHoliday SET FestivalName=@p0, FromDate=@p1, ToDate=@p2, Notes=@p3 WHERE HolidayID=@p4",
                    ("@p0", input.FestivalName.Trim()), ("@p1", input.FromDate), ("@p2", input.ToDate),
                    ("@p3", input.Notes.Trim()), ("@p4", input.HolidayId.Value));
            }
            else
            {
                DbHelper.ExecuteNonQuery(
                    "INSERT INTO dbo.CompanyHoliday (FestivalName, FromDate, ToDate, Notes) VALUES (@p0,@p1,@p2,@p3)",
                    ("@p0", input.FestivalName.Trim()), ("@p1", input.FromDate), ("@p2", input.ToDate),
                    ("@p3", input.Notes.Trim()));
            }
            return (true, null);
        }

        public static void Delete(int holidayId)
        {
            EnsureTable();
            DbHelper.ExecuteNonQuery("DELETE FROM dbo.CompanyHoliday WHERE HolidayID = @p0", ("@p0", holidayId));
        }
    }
}
