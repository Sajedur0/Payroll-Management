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
        public static List<CompanyHolidayRow> List() =>
            DbHelper.FetchRows(
                "SELECT HolidayID, FestivalName, FromDate, ToDate, Notes FROM CompanyHoliday ORDER BY FromDate")
            .Select(r => new CompanyHolidayRow(
                Convert.ToInt32(r[0]), r[1]?.ToString() ?? "", r[2]?.ToString() ?? "",
                r[3]?.ToString() ?? "", r[4]?.ToString() ?? "")).ToList();

        public static (bool ok, string? error) Save(CompanyHolidayInput input)
        {
            if (string.IsNullOrWhiteSpace(input.FestivalName))
                return (false, "Festival name is required");
            if (string.CompareOrdinal(input.FromDate, input.ToDate) > 0)
                return (false, "From date cannot be after To date");

            if (input.HolidayId.HasValue)
            {
                DbHelper.ExecuteNonQuery(
                    "UPDATE CompanyHoliday SET FestivalName=@p0, FromDate=@p1, ToDate=@p2, Notes=@p3 WHERE HolidayID=@p4",
                    ("@p0", input.FestivalName.Trim()), ("@p1", input.FromDate), ("@p2", input.ToDate),
                    ("@p3", input.Notes.Trim()), ("@p4", input.HolidayId.Value));
            }
            else
            {
                DbHelper.ExecuteNonQuery(
                    "INSERT INTO CompanyHoliday (FestivalName, FromDate, ToDate, Notes) VALUES (@p0,@p1,@p2,@p3)",
                    ("@p0", input.FestivalName.Trim()), ("@p1", input.FromDate), ("@p2", input.ToDate),
                    ("@p3", input.Notes.Trim()));
            }
            return (true, null);
        }

        public static void Delete(int holidayId) =>
            DbHelper.ExecuteNonQuery("DELETE FROM CompanyHoliday WHERE HolidayID = @p0", ("@p0", holidayId));
    }
}
