using PayrollManagement.Data;

namespace PayrollManagement.AdminTools
{
    public class CompanyDetailsInput
    {
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public string Number { get; set; } = "";
    }

    public static class CompanyDetailsService
    {
        public static CompanyDetails Load() => CompanyHelper.GetCompanyDetails();

        public static void Save(CompanyDetailsInput input) =>
            DbHelper.ExecuteNonQuery(
                "UPDATE CompanyDetails SET Name = @p0, Address = @p1, Number = @p2 WHERE ID = 1",
                ("@p0", input.Name.Trim()), ("@p1", input.Address.Trim()), ("@p2", input.Number.Trim()));

        public static void EnsureTable()
        {
            DbHelper.ExecuteNonQuery(@"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='CompanyDetails')
                CREATE TABLE CompanyDetails (ID INT PRIMARY KEY CHECK (ID=1), Name NVARCHAR(200), Address NVARCHAR(500), Number NVARCHAR(50));
                IF NOT EXISTS (SELECT 1 FROM CompanyDetails WHERE ID=1) INSERT INTO CompanyDetails (ID, Name, Address, Number) VALUES (1, 'PAYROLL SYSTEM', '', '');
            ");
        }
    }
}
