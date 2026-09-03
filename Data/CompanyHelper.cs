namespace PayrollManagement.Data
{
    public record CompanyDetails(string Name, string Address, string Number);

    public static class CompanyHelper
    {
        public static CompanyDetails GetCompanyDetails()
        {
            try
            {
                var rows = DbHelper.FetchRows("SELECT Name, Address, Number FROM CompanyDetails WHERE ID = 1");
                if (rows.Count == 0)
                    return new CompanyDetails("PAYROLL SYSTEM", "", "");
                var r = rows[0];
                return new CompanyDetails(
                    r[0] as string ?? "PAYROLL SYSTEM",
                    r[1] as string ?? "",
                    r[2] as string ?? "");
            }
            catch
            {
                return new CompanyDetails("PAYROLL SYSTEM", "", "");
            }
        }
    }
}
